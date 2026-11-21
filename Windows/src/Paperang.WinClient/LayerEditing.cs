using System.Text.Json;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Paperang.Core;
using Paperang.Windows;

namespace Paperang.WinClient;

internal sealed record LayerSource(string Kind, string Text = "", string Rtf = "", WorkspaceSettings? Settings = null,
	string Format = "纯文字", string Shape = "方形", string Correction = "M", bool Border = false, string ImageBase64 = "");

internal sealed partial class WorkspaceWindow
{
	private Action<string>? selectEditorGroup;
	private PaperLayer[] confirmedLayers = [];
	private int confirmedHeight = 720;
	private bool layoutPending;
	private readonly RichEditBox layerText = new() { Height = 180, IsSpellCheckEnabled = false, AcceptsReturn = true };
	private readonly ComboBox layerTypeface = Choice("选区字体", "Microsoft YaHei", "Arial", "Consolas");
	private readonly NumberBox layerPointSize = Number("选区字号（pt）", 6, 72, 18);
	private readonly CheckBox layerBold = new() { Content = "粗体" };
	private readonly CheckBox layerItalic = new() { Content = "斜体" };
	private readonly CheckBox layerUnderline = new() { Content = "下划线" };
	private readonly ComboBox layerConversion = Choice("对象黑白方式", "阈值", "Floyd–Steinberg", "有序抖动");
	private readonly NumberBox layerThreshold = Number("对象黑白阈值", 0, 255, 128);
	private readonly NumberBox layerBrightness = Number("对象亮度", -100, 100, 0);
	private readonly NumberBox layerContrast = Number("对象对比度", 0, 3, 1);
	private readonly NumberBox layerGamma = Number("对象 Gamma", 0, 3, 1);
	private readonly CheckBox layerInvert = new() { Content = "对象黑白反相" };

	private JsonElement? CaptureLayerSource()
	{
		LayerSource value = sourceMode switch
		{
			"text" => new("text", text.Text, Settings: ReadWorkspaceSettings()),
			"qr" => new("qr", qrText.Text, Format: (string)qrFormat.SelectedItem, Shape: (string)qrShape.SelectedItem,
				Correction: (string)qrCorrection.SelectedItem, Border: qrBorder.IsChecked == true),
			"image" => new("image", Settings: ReadWorkspaceSettings(), ImageBase64: Convert.ToBase64String(
				WorkspaceImage.CaptureGray(sourceImagePath ?? throw new InvalidDataException("图片来源已丢失。"), ReadWorkspaceSettings()))),
			_ => new("raster")
		};
		return JsonSerializer.SerializeToElement(value, RelayClient.Json);
	}
	private static LayerSource? ReadLayerSource(PaperLayer layer) =>
		layer.Source?.Deserialize<LayerSource>(RelayClient.Json);

	private void BuildLayerEditor(StackPanel controls)
	{
		layerContrast.Minimum = layerGamma.Minimum = .2;
		layerContrast.SmallChange = layerGamma.SmallChange = .05;
		var tones = new StackPanel { Spacing = 8 };
		foreach (var field in new FrameworkElement[] { layerConversion, layerThreshold, layerBrightness, layerContrast, layerGamma, layerInvert }) tones.Children.Add(field);
		tones.Children.Add(ActionButton("应用对象灰度", async _ =>
		{
			int index = SelectedLayer();
			var original = layers[index];
			var source = ReadLayerSource(original);
			if (source?.Kind is not ("image" or "text" or "rich" or "label"))
				throw new InvalidOperationException("请选择保留原始内容的图片或文字对象。旧点阵和二维码不应用灰度变换。");
			var settings = (source.Settings ?? new()) with { Conversion = (string)layerConversion.SelectedItem,
				Threshold = layerThreshold.Value, Brightness = layerBrightness.Value, Contrast = layerContrast.Value,
				Gamma = layerGamma.Value, Invert = layerInvert.IsChecked == true };
			settings.Validate();
			var updated = RenderEditableLayer(original with { Source = JsonSerializer.SerializeToElement(source with { Settings = settings }, RelayClient.Json) });
			var rendered = PaperLayout.Render(Value(pageHeight), layers.Select((item, position) => position == index ? updated : item));
			layers[index] = updated;
			await ShowLayout(rendered);
		}));
		controls.Children.Add(PropertyGroup("对象灰度", tones));
		var additions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		additions.Children.Add(ActionButton("添加文字框", _ => AddTextLayer(false)));
		additions.Children.Add(ActionButton("添加标签", _ => AddTextLayer(true)));
		controls.Children.Add(additions);
		controls.Children.Add(Paragraph("选中文字范围后应用字体样式；内容和样式只有点击“应用对象文字”后才写入纸面。"));
		controls.Children.Add(layerText);
		controls.Children.Add(TwoColumns(layerTypeface, layerPointSize));
		var styles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		styles.Children.Add(layerBold); styles.Children.Add(layerItalic); styles.Children.Add(layerUnderline);
		controls.Children.Add(styles);
		controls.Children.Add(SimpleAction("应用到选区", () =>
		{
			if (!double.IsFinite(layerPointSize.Value) || layerPointSize.Value < 6 || layerPointSize.Value > 72)
				throw new InvalidDataException("选区字号须为 6–72 pt。");
			var selection = layerText.Document.Selection;
			var format = selection.CharacterFormat;
			format.Name = (string)layerTypeface.SelectedItem;
			format.Size = (float)layerPointSize.Value;
			format.Bold = layerBold.IsChecked == true ? FormatEffect.On : FormatEffect.Off;
			format.Italic = layerItalic.IsChecked == true ? FormatEffect.On : FormatEffect.Off;
			format.Underline = layerUnderline.IsChecked == true ? UnderlineType.Single : UnderlineType.None;
			selection.CharacterFormat = format;
		}));
		var alignment = Choice("对象文字对齐", "左对齐", "居中", "右对齐");
		controls.Children.Add(alignment);
		controls.Children.Add(SimpleAction("应用段落对齐", () =>
		{
			var paragraph = layerText.Document.Selection.ParagraphFormat;
			paragraph.Alignment = alignment.SelectedIndex switch { 0 => ParagraphAlignment.Left, 2 => ParagraphAlignment.Right, _ => ParagraphAlignment.Center };
			layerText.Document.Selection.ParagraphFormat = paragraph;
		}));
		controls.Children.Add(ActionButton("应用对象文字", async _ =>
		{
			int index = SelectedLayer();
			var previous = ReadLayerSource(layers[index]);
			if (previous?.Kind is not ("rich" or "label" or "text"))
				throw new InvalidOperationException("所选对象不是文字，请先添加文字框，或载入对应工具编辑。");
			layerText.Document.GetText(TextGetOptions.None, out string plain);
			if (string.IsNullOrWhiteSpace(plain)) throw new InvalidDataException("对象文字不能为空。");
			layerText.Document.GetText(TextGetOptions.FormatRtf, out string rtf);
			var source = new LayerSource(previous.Kind == "label" ? "label" : "rich", plain.TrimEnd('\r', '\n'), rtf.TrimEnd('\0'), ReadWorkspaceSettings());
			var updated = RenderEditableLayer(layers[index] with { Source = JsonSerializer.SerializeToElement(source, RelayClient.Json) });
			var composed = PaperLayout.Render(Value(pageHeight), layers.Select((layer, position) => position == index ? updated : layer));
			layers[index] = updated;
			await ShowLayout(composed);
		}));
		controls.Children.Add(ActionButton("载入所选对象到编辑工具", async token =>
		{
			var source = ReadLayerSource(layers[SelectedLayer()]) ?? throw new InvalidOperationException("此对象只有位图，可导入新内容后替换。");
			if (source.Kind is "rich" or "label") { layerText.Focus(FocusState.Programmatic); return; }
			if (source.Kind == "text")
			{
				text.Text = source.Text;
				if (source.Settings != null) ApplyWorkspaceSettings(source.Settings);
				sourceMode = "text";
				selectEditorGroup?.Invoke("文字");
			}
			else if (source.Kind == "qr")
			{
				applyingSettings = true;
				try
				{
					qrText.Text = source.Text; qrFormat.SelectedItem = source.Format; qrShape.SelectedItem = source.Shape;
					qrCorrection.SelectedItem = source.Correction; qrBorder.IsChecked = source.Border;
				}
				finally { applyingSettings = false; }
				sourceMode = "qr";
				selectEditorGroup?.Invoke("二维码");
			}
			else throw new InvalidOperationException("此对象为图片点阵，请选择新图片预览后替换。");
			await RenderActiveAsync(token);
		}));
		controls.Children.Add(ActionButton("用当前预览替换所选对象", async _ =>
		{
			if (layoutShown) throw new InvalidOperationException("请先单独预览新内容，再替换对象。");
			int index = SelectedLayer();
			var updated = layers[index] with { Image = RequirePreview(), SourceWidth = 576, Source = CaptureLayerSource() };
			var composed = PaperLayout.Render(Value(pageHeight), layers.Select((layer, position) => position == index ? updated : layer));
			layers[index] = updated;
			await ShowLayout(composed);
		}));
		controls.Children.Add(ActionButton("确认并渲染排版", async _ => await ConfirmLayout()));
		controls.Children.Add(ActionButton("取消本次排版更改", async _ =>
		{
			layers.Clear(); layers.AddRange(confirmedLayers); pageHeight.Value = confirmedHeight;
			UpdateLayerList(layers.Count - 1);
			if (layers.Count > 0) { await ShowLayout(); layoutPending = false; }
			else
			{
				sourceMode = "text"; raster = null; paper.Source = null; layoutShown = false; layoutPending = false;
				previewDirty = false; paperHint.Text = "尚无已确认的排版";
			}
		}));
	}
	private async Task AddTextLayer(bool label)
	{
		if (layers.Count >= 24) throw new InvalidOperationException("最多添加 24 个对象。");
		var format = layerText.Document.GetDefaultCharacterFormat();
		format.Name = "Microsoft YaHei"; format.Size = 18;
		layerText.Document.SetDefaultCharacterFormat(format);
		string text = label ? "标签" : "输入文字";
		layerText.Document.SetText(TextSetOptions.None, text);
		layerText.Document.GetText(TextGetOptions.FormatRtf, out string rtf);
		var source = new LayerSource(label ? "label" : "rich", text, rtf.TrimEnd('\0'), ReadWorkspaceSettings());
		int top = layers.Count == 0 ? 0 : layers.Max(layer => layer.Y + layer.Height) + 8;
		if (top + 100 > 1400) throw new InvalidOperationException("纸面已满，请先调整或删除对象。");
		var added = RenderEditableLayer(new(new Raster(100, new byte[7200]), 0, top, 280, 100, Source: JsonSerializer.SerializeToElement(source, RelayClient.Json)));
		int length = Math.Max(Value(pageHeight), top + 100);
		PaperLayout.Render(length, layers.Append(added));
		pageHeight.Value = length;
		layers.Add(added); UpdateLayerList(layers.Count - 1);
		await ShowLayout();
	}
	private async Task ConfirmLayout()
	{
		if (layers.Count == 0) throw new InvalidOperationException("请先添加排版对象。");
		await ShowLayout();
		confirmedLayers = layers.ToArray(); confirmedHeight = Value(pageHeight); layoutPending = false;
	}
	private void LoadLayerEditor(PaperLayer layer)
	{
		var source = ReadLayerSource(layer);
		var tone = source?.Settings ?? new();
		layerConversion.SelectedItem = tone.Conversion; layerThreshold.Value = tone.Threshold;
		layerBrightness.Value = tone.Brightness; layerContrast.Value = tone.Contrast;
		layerGamma.Value = tone.Gamma; layerInvert.IsChecked = tone.Invert;
		layerText.IsReadOnly = source?.Kind is not ("rich" or "label" or "text");
		if (source?.Kind is "rich" or "label") layerText.Document.SetText(TextSetOptions.FormatRtf, source.Rtf);
		else
		{
			var format = layerText.Document.GetDefaultCharacterFormat();
			format.Name = source?.Settings?.FontName ?? "Microsoft YaHei";
			format.Size = (float)((source?.Settings?.FontSize ?? 24) * .75);
			layerText.Document.SetDefaultCharacterFormat(format);
			layerText.Document.SetText(TextSetOptions.None, source?.Text ?? "");
		}
	}
	private PaperLayer RenderEditableLayer(PaperLayer layer)
	{
		var source = ReadLayerSource(layer);
		if (source?.Kind is "rich" or "label")
			return layer with { Image = RichTextRenderer.Render(source.Rtf, layer.Width, layer.Height,
				source.Settings ?? new(), source.Kind == "label"), SourceWidth = layer.Width };
		if (source?.Kind == "text")
		{
			var settings = source.Settings ?? new();
			var rendered = NoteRenderer.RenderText(source.Text, settings.FontName, (float)settings.FontSize,
				settings.Bold, settings.Alignment, 0, 0, (float)settings.LineSpacing, settings, layer.Width, localBox: true);
			if (rendered.Height > layer.Height) throw new InvalidDataException("文字超出对象范围，请增加高度。");
			var raster = new Raster(layer.Height, new byte[layer.Height * 72]);
			rendered.Bytes.CopyTo(raster.Bytes, 0);
			return layer with { Image = raster, SourceWidth = layer.Width };
		}
		if (source?.Kind == "qr")
		{
			var rendered = WorkspaceFiles.Qr(source.Text, Math.Min(layer.Width, layer.Height), source.Correction,
				source.Format, source.Shape, source.Border);
			int side = rendered.Height, start = (576 - side) / 2;
			var raster = new Raster(layer.Height, new byte[layer.Height * 72]);
			for (int row = 0; row < side; row++)
				for (int column = 0; column < side; column++)
					if ((rendered.Bytes[row * 72 + (start + column) / 8] & (128 >> ((start + column) % 8))) != 0)
					{
						int target = (layer.Width - side) / 2 + column;
						raster.Bytes[((layer.Height - side) / 2 + row) * 72 + target / 8] |= (byte)(128 >> (target % 8));
					}
			return layer with { Image = raster, SourceWidth = layer.Width };
		}
		if (source?.Kind == "image")
		{
			if (layer.Width < 48 || layer.Height < 24) throw new InvalidDataException("可编辑图片对象最小为 48×24 点。");
			var settings = (source.Settings ?? new()) with { ContentWidth = layer.Width, MarginX = 0, MarginY = 0,
				ImageFit = "裁切填满", CropHeight = layer.Height, Rotation = 0, Alignment = "居左" };
			var rendered = WorkspaceImage.Render(Convert.FromBase64String(source.ImageBase64), settings);
			return layer with { Image = rendered, SourceWidth = layer.Width };
		}
		return layer;
	}
}
