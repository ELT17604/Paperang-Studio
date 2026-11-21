using System.Drawing.Text;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Paperang.Core;
using Paperang.Windows;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private readonly NumberBox contentWidth = Number("内容宽度（px）", 48, 576, 576);
	private readonly ComboBox imageFit = Choice("缩放方式", "适配宽度", "保持像素", "裁切填满");
	private readonly ComboBox rotation = Choice("旋转", "0", "90", "180", "270");
	private readonly NumberBox cropHeight = Number("裁切高度（px）", 24, 1400, 400);
	private readonly ComboBox conversion = Choice("黑白方式", "阈值", "Floyd–Steinberg", "有序抖动");
	private readonly ComboBox qrFormat = Choice("内容格式", "纯文字", "网页链接", "电子邮件");
	private readonly ComboBox qrShape = Choice("模块形态", "方形", "圆角", "圆点");
	private readonly CheckBox qrBorder = new() { Content = "加外框" };
	private readonly NumberBox brightness = Number("亮度", -100, 100, 0);
	private readonly NumberBox contrast = Number("对比度", 0, 3, 1);
	private readonly NumberBox gamma = Number("Gamma", 0, 3, 1);
	private readonly CheckBox invert = new() { Content = "黑白反相" };
	private readonly TextBlock imageName = Paragraph("尚未选择图片");
	private readonly ComboBox quickPreset = Choice("快捷预设", "默认", "照片", "标签", "线稿");
	private readonly NumberBox transferFrame = Number("图像载荷（bytes，72 的倍数）", 72, 1008, 1008);
	private readonly NumberBox transferChunk = Number("BLE 分段上限（bytes）", 20, 180, 180);
	private readonly NumberBox transferWrite = Number("BLE 写入间隔（ms）", 5, 200, 10);
	private readonly NumberBox transferPause = Number("图像帧间隔（ms）", 0, 1000, 0);
	private readonly NumberBox transferTimeout = Number("单命令超时（秒）", 3, 30, 6);
	private readonly NumberBox transferFinish = Number("数据后等待（秒）", 1, 15, 2);
	private DispatcherQueueTimer? previewTimer;
	private string sourceMode = "text";
	private string? sourceImagePath;
	private Raster? importedRaster;
	private int previewRevision;
	private bool previewDirty;
	private bool applyingSettings;
	private bool suppressAutomaticPreview = false;
	private static readonly JsonSerializerOptions PresetJson = new(RelayClient.Json) { WriteIndented = true };

	private WorkspaceSettings ReadWorkspaceSettings() => new WorkspaceSettings
	{
		FontName = (string)fontFamily.SelectedItem, FontSize = font.Value, Bold = bold.IsChecked == true,
		LineSpacing = spacing.Value, Alignment = (string)alignment.SelectedItem, MarginX = Value(margin),
		MarginY = Value(marginY), ContentWidth = Value(contentWidth), ImageFit = (string)imageFit.SelectedItem,
		Rotation = int.Parse((string)rotation.SelectedItem), CropHeight = Value(cropHeight),
		Conversion = (string)conversion.SelectedItem, Threshold = threshold.Value, Brightness = brightness.Value,
		Contrast = contrast.Value, Gamma = gamma.Value, Invert = invert.IsChecked == true,
		Density = Value(density), Copies = Value(copies), FeedBefore = Value(before), FeedAfter = Value(feed),
		FeedBetweenCopies = between.IsChecked == true, PacketBytes = Value(transferFrame), BleChunk = Value(transferChunk),
		WritePauseMS = Value(transferWrite), FramePauseMS = Value(transferPause), TimeoutSeconds = Value(transferTimeout),
		FinishWaitSeconds = Value(transferFinish)
	}.Validate();

	private void InitializeEditing()
	{
		using var fonts = new InstalledFontCollection();
		fontFamily.ItemsSource = fonts.Families.SelectMany(family => new[] { family.Name, family.GetName(0x409) })
			.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToArray();
		fontFamily.SelectedItem = "Microsoft YaHei";
		if (fontFamily.SelectedIndex < 0) fontFamily.SelectedIndex = 0;
		layerTypeface.ItemsSource = fontFamily.ItemsSource;
		layerTypeface.SelectedItem = fontFamily.SelectedItem;
		contrast.Minimum = gamma.Minimum = .2;
		transferFrame.SmallChange = 72;
		previewTimer = DispatcherQueue.CreateTimer();
		previewTimer.Interval = TimeSpan.FromMilliseconds(220);
		previewTimer.IsRepeating = false;
		previewTimer.Tick += async (_, _) =>
		{
			if (operation != null || suppressAutomaticPreview) return;
			try { await RenderActiveAsync(default); }
			catch (Exception error) { Report(error.Message, InfoBarSeverity.Error); }
		};
		text.TextChanged += (_, _) =>
		{
			if (applyingSettings || sourceMode != "text" && text.FocusState == Microsoft.UI.Xaml.FocusState.Unfocused) return;
			sourceMode = "text";
			RequestPreview();
		};
		qrText.TextChanged += (_, _) => { if (!applyingSettings && (sourceMode == "qr" || qrText.FocusState != Microsoft.UI.Xaml.FocusState.Unfocused)) { sourceMode = "qr"; RequestPreview(); } };
		foreach (var field in new[] { font, margin, marginY, spacing, contentWidth, threshold, brightness, contrast, gamma, cropHeight, qrWidth })
			field.ValueChanged += (_, _) => RequestPreview();
		foreach (var choice in new[] { fontFamily, alignment, imageFit, rotation, conversion, qrCorrection, qrFormat, qrShape })
			choice.SelectionChanged += (_, _) => RequestPreview();
		foreach (var choice in new[] { bold, invert, trim, qrBorder })
			choice.Click += (_, _) => RequestPreview();
		Closed += (_, _) => previewTimer.Stop();
	}

	private void RequestPreview()
	{
		if (applyingSettings || suppressAutomaticPreview || previewTimer == null) return;
		if (!HasActiveContent())
		{
			previewRevision++; previewTimer.Stop(); previewDirty = false; raster = null; paper.Source = null;
			paperHint.Text = "添加内容后将自动显示预览";
			return;
		}
		previewRevision++;
		previewDirty = true;
		previewTimer.Stop();
		previewTimer.Start();
	}

	private async Task RenderActiveAsync(CancellationToken token)
	{
		previewTimer?.Stop();
		int revision = previewRevision;
		var settings = ReadWorkspaceSettings();
		string mode = sourceMode, value = text.Text, qrValue = qrText.Text;
		string correction = (string)qrCorrection.SelectedItem;
		string format = (string)qrFormat.SelectedItem, shape = (string)qrShape.SelectedItem;
		bool border = qrBorder.IsChecked == true;
		int qrSize = Value(qrWidth);
		string? path = sourceImagePath;
		var savedRaster = importedRaster;
		var composition = layers.ToArray();
		int length = Value(pageHeight);
		var next = await Task.Run(() => mode switch
		{
			"image" => WorkspaceFiles.Open(path ?? throw new InvalidDataException("请先选择图片。"), settings),
			"raster" => savedRaster ?? throw new InvalidDataException("请先导入位图。"),
			"qr" => WorkspaceFiles.Qr(qrValue, qrSize, correction, format, shape, border),
			"layout" => PaperLayout.Render(length, composition),
			_ => string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException("请输入纸条正文。") :
				NoteRenderer.RenderText(value, settings.FontName, (float)settings.FontSize, settings.Bold,
					settings.Alignment, settings.MarginX, settings.MarginY, (float)settings.LineSpacing, settings, settings.ContentWidth)
		}, token);
		token.ThrowIfCancellationRequested();
		if (revision != previewRevision) return;
		var draft = mode == "text" ? new TextDraft(value, settings.FontName, (int)settings.FontSize, settings.Bold,
			settings.Alignment, settings.MarginX, (int)settings.LineSpacing, settings.MarginY) : null;
		await Preview(next, draft, applyTrim: mode is "text" or "image", expectedRevision: revision);
		if (mode == "layout" && revision == previewRevision) layoutShown = true;
	}

	private void ApplyWorkspaceSettings(WorkspaceSettings settings)
	{
		settings.Validate();
		applyingSettings = true;
		try
		{
			string family = settings.FontName.ToLowerInvariant() switch { "pingfang sc" or "sans-serif" or "sans" => "Microsoft YaHei", "menlo" or "monospace" => "Consolas", "helvetica" => "Arial", "serif" => "SimSun", _ => settings.FontName };
			if (fontFamily.ItemsSource is string[] families && !families.Contains(family))
				family = families.Contains("Microsoft YaHei") ? "Microsoft YaHei" : families[0];
			fontFamily.SelectedItem = family;
			font.Value = settings.FontSize; bold.IsChecked = settings.Bold; spacing.Value = settings.LineSpacing;
			alignment.SelectedItem = settings.Alignment; margin.Value = settings.MarginX; marginY.Value = settings.MarginY;
			contentWidth.Value = settings.ContentWidth; imageFit.SelectedItem = settings.ImageFit;
			rotation.SelectedItem = settings.Rotation.ToString(); cropHeight.Value = settings.CropHeight;
			conversion.SelectedItem = settings.Conversion; threshold.Value = settings.Threshold;
			brightness.Value = settings.Brightness; contrast.Value = settings.Contrast; gamma.Value = settings.Gamma;
			invert.IsChecked = settings.Invert; density.Value = settings.Density; copies.Value = settings.Copies;
			before.Value = settings.FeedBefore; feed.Value = settings.FeedAfter; between.IsChecked = settings.FeedBetweenCopies;
			transferFrame.Value = settings.PacketBytes; transferChunk.Value = settings.BleChunk;
			transferWrite.Value = settings.WritePauseMS; transferPause.Value = settings.FramePauseMS;
			transferTimeout.Value = settings.TimeoutSeconds; transferFinish.Value = settings.FinishWaitSeconds;
		}
		finally { applyingSettings = false; }
		RequestPreview();
	}

	private async Task PresetFile(bool save, CancellationToken token)
	{
		string? path;
		if (save)
		{
			var picker = new FileSavePicker { SuggestedFileName = "Paperang-preset" };
			picker.FileTypeChoices.Add("参数预设 JSON", new List<string> { ".json" });
			InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
			path = (await picker.PickSaveFileAsync())?.Path;
		}
		else
		{
			var picker = new FileOpenPicker();
			picker.FileTypeFilter.Add(".json");
			InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
			path = (await picker.PickSingleFileAsync())?.Path;
		}
		if (path == null) return;
		if (save)
			await File.WriteAllTextAsync(path, JsonSerializer.Serialize(ReadWorkspaceSettings(), PresetJson), token);
		else
		{
			if (new FileInfo(path).Length > 128_000) throw new InvalidDataException("预设文件超过大小上限。");
			var settings = (await Task.Run(() => WorkspaceJson.Read(path), token)).Settings
				?? throw new InvalidDataException("这是点阵文件，请使用“导入 JSON”载入内容。");
			ApplyWorkspaceSettings(settings);
			if (HasActiveContent()) await RenderActiveAsync(token);
		}
		Report(save ? "预设已保存。" : "预设已应用，尚未修改打印机。", InfoBarSeverity.Success);
	}
}
