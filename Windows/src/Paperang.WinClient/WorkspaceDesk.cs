using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Paperang.Core;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private readonly TextBlock paperHint = Paragraph("576 点宽 · 生成文字、图片或二维码预览后，纸面将在这里显示。");
	private readonly TextBlock connectionStatus = Paragraph("未连接打印机");
	private readonly TextBlock relayStatus = Paragraph("尚未连接中转服务器");
	private readonly ProgressBar deviceProgress = new() { Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };
	private readonly ListView sessionJobs = new() { Header = "本次运行的打印任务", MaxHeight = 240 };
	private readonly Border previewSheet = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.White),
		BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 215, 218, 224)),
		BorderThickness = new Thickness(1), MinWidth = 576, MinHeight = 720, HorizontalAlignment = HorizontalAlignment.Center,
		VerticalAlignment = VerticalAlignment.Top };
	private readonly NumberBox pageHeight = Number("纸张高度（点）", 1, 1400, 720);
	private readonly NumberBox layerX = Number("X · 左边（点）", 0, 575, 0);
	private readonly NumberBox layerY = Number("Y · 上边（点）", 0, 1399, 0);
	private readonly NumberBox layerWidth = Number("图层宽度（点）", 1, 576, 576);
	private readonly NumberBox layerHeight = Number("图层高度（点）", 1, 1400, 1);
	private readonly ListView layerList = new() { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 168 };
	private readonly List<PaperLayer> layers = [];
	private bool layoutShown;
	private FrameworkElement? editingSurface, previewSurface;
	private (int Index, double X, double Y)? drag;

	private readonly TextBlock importedContent = Paragraph("导入 Mac / Android 点阵 JSON 或 RAW 文件，保留其实际打印像素。");
	private readonly TextBlock printSummary = Paragraph("编辑内容后可打印；预览会自动更新。");
	private void RefreshPrintSummary() => printSummary.Text = (trayHost?.AllowPaper == true ? "允许打印" : "耗纸已暂停 · 请开启左侧开关") +
		(raster == null ? " · 等待添加内容" : $" · 576 × {raster.Height} 点 · {copies.Value:0} 份");
	private bool HasActiveContent() => sourceMode switch
	{
		"image" => sourceImagePath != null, "raster" => importedRaster != null,
		"qr" => !string.IsNullOrWhiteSpace(qrText.Text), "layout" => layers.Count > 0,
		_ => !string.IsNullOrWhiteSpace(text.Text)
	};
	private void ActivateEditor(string mode)
	{
		if (sourceMode == mode) return;
		sourceMode = mode;
		if (HasActiveContent()) RequestPreview();
		else
		{
			previewRevision++; previewTimer?.Stop(); previewDirty = false;
			raster = null; paper.Source = null; paperHint.Text = "添加内容后将自动显示预览";
		}
	}
	private async Task ImportJson(CancellationToken token)
	{
		var picker = new global::Windows.Storage.Pickers.FileOpenPicker();
		picker.FileTypeFilter.Add(".json");
		WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
		var file = await picker.PickSingleFileAsync();
		if (file != null) await ImportPath(file.Path, file.Name, token);
	}
	private void BuildDesk()
	{
		text.MinHeight = 180; text.MaxHeight = 320;
		var commands = new CommandBar { DefaultLabelPosition = CommandBarDefaultLabelPosition.Right,
			IsDynamicOverflowEnabled = true, Content = Heading("打印工作台"), MinHeight = 56 };
		Command("打印…", Symbol.Print, Print);
		Command("导入文件…", Symbol.OpenFile, Import);
		var json = new AppBarButton { Label = "导入 JSON", Icon = new SymbolIcon(Symbol.Document) };
		BindAction(json, ImportJson); commands.PrimaryCommands.Add(json);
		Command("导出…", Symbol.Save, Export);
		PresetCommand("导入参数预设", false); PresetCommand("保存参数预设", true);
		var abort = new AppBarButton { Label = "取消发送", Icon = new SymbolIcon(Symbol.Cancel), IsEnabled = false };
		abort.Click += (_, _) => operation?.Cancel();
		commands.SecondaryCommands.Add(abort);
		cancel.RegisterPropertyChangedCallback(Control.IsEnabledProperty, (_, _) => abort.IsEnabled = cancel.IsEnabled);
		document.RowDefinitions.Add(new() { Height = GridLength.Auto });
		document.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
		document.RowDefinitions.Add(new() { Height = GridLength.Auto });
		document.Margin = new Thickness(24, 12, 24, 20); document.Children.Add(commands);

		var originalSections = inspector.Children.OfType<Border>().ToList();
		inspector.Children.Clear();
		var rasterControls = new StackPanel { Spacing = 10 };
		rasterControls.Children.Add(importedContent);
		rasterControls.Children.Add(ActionButton("导入点阵 JSON…", ImportJson));
		originalSections.Add((Border)PropertyGroup("导入点阵", rasterControls));
		var tone = originalSections.Single(section => (string)section.Tag == "黑白处理");
		var sections = originalSections.Where(section => section != tone).ToList();
		var mode = Choice("内容类型", sections.Select(section => (string)section.Tag).ToArray());
		var editor = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
		selectEditorGroup = name => mode.SelectedIndex = sections.FindIndex(section => (string)section.Tag == name);
		mode.SelectionChanged += (_, _) =>
		{
			if (mode.SelectedIndex < 0) return;
			editor.Content = sections[mode.SelectedIndex];
			string? active = (string)sections[mode.SelectedIndex].Tag switch
			{
				"文字" => "text", "图片" => "image", "二维码" => "qr",
				"导入点阵" => "raster", _ => null
			};
			if (active != null) ActivateEditor(active);
		};
		editor.Content = sections[0];
		inspector.Children.Add(mode); inspector.Children.Add(editor); mode.SelectedIndex = 0;
		inspector.Children.Add(CollapsibleGroup("黑白与图片处理", tone));
		var presets = new StackPanel { Spacing = 10 };
		presets.Children.Add(quickPreset);
		presets.Children.Add(ActionButton("应用快捷预设", async token =>
		{
			ApplyWorkspaceSettings(WorkspaceSettings.Preset((string)quickPreset.SelectedItem));
			if (HasActiveContent()) await RenderActiveAsync(token);
		}));
		presets.Children.Add(TwoColumns(ActionButton("导入预设 JSON…", token => PresetFile(false, token)),
			ActionButton("保存预设…", token => PresetFile(true, token))));
		inspector.Children.Add(CollapsibleGroup("参数预设", presets));
		var printing = new StackPanel { Spacing = 12 };
		printing.Children.Add(connectionStatus); printing.Children.Add(deviceProgress);
		printing.Children.Add(TwoColumns(density, copies)); printing.Children.Add(TwoColumns(before, feed));
		printing.Children.Add(between); printing.Children.Add(keepHistory);
		inspector.Children.Add(PropertyGroup("打印设置", printing));
		var properties = new ScrollViewer { Content = inspector, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
		editingSurface = properties;
		var regions = new Grid { ColumnSpacing = 20, RowSpacing = 12, Margin = new Thickness(0, 12, 0, 12) };
		regions.ColumnDefinitions.Add(new() { Width = new GridLength(2, GridUnitType.Star) });
		var previewColumn = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 360 };
		regions.ColumnDefinitions.Add(previewColumn);
		regions.RowDefinitions.Add(new());
		regions.RowDefinitions.Add(new() { Height = new GridLength(0) });
		regions.Children.Add(properties);

		var preview = new Grid { RowSpacing = 8 };
		previewSurface = preview;
		preview.RowDefinitions.Add(new() { Height = GridLength.Auto });
		preview.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
		preview.RowDefinitions.Add(new() { Height = GridLength.Auto });
		var heading = new StackPanel { Spacing = 6 };
		heading.Children.Add(new TextBlock { Text = "打印预览", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
		heading.Children.Add(paperHint); preview.Children.Add(heading);
		AutomationProperties.SetName(paper, "打印预览；排版模式中可拖动对象");
		paper.HorizontalAlignment = HorizontalAlignment.Center; paper.VerticalAlignment = VerticalAlignment.Top;
		paper.PointerPressed += StartLayerDrag; paper.PointerReleased += EndLayerDrag;
		paper.PointerCaptureLost += (_, _) => drag = null;
		previewSheet.Child = paper;
		var paperScroll = new ScrollViewer { Content = previewSheet, HorizontalScrollMode = ScrollMode.Enabled,
			ZoomMode = ZoomMode.Enabled, MinZoomFactor = .15f, MaxZoomFactor = 3, Padding = new Thickness(12),
			Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 243, 245, 248)),
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
		Grid.SetRow(paperScroll, 1); preview.Children.Add(paperScroll);
		paperScroll.SizeChanged += (_, _) => FitPaper();
		var enlarge = ActionButton("放大预览…", async _ =>
		{
			if (previewDirty && HasActiveContent()) await RenderActiveAsync(default);
			var image = new Microsoft.UI.Xaml.Controls.Image { Source = await RasterImage(RequirePreview()), MaxWidth = 576, Stretch = Stretch.Uniform };
			await new ContentDialog { Title = "打印预览", Content = new ScrollViewer { Content = image, MaxHeight = 600 },
				CloseButtonText = "返回编辑", XamlRoot = document.XamlRoot }.ShowAsync();
		});
		var previewActions = TwoColumns(ActionButton("刷新预览", RenderActiveAsync), enlarge);
		preview.Children.Add(previewActions); Grid.SetRow(previewActions, 2);
		regions.Children.Add(preview); Grid.SetColumn(preview, 1);
		bool? wasWide = null;
		regions.SizeChanged += (_, args) =>
		{
			bool wide = args.NewSize.Width >= 860;
			if (wide == wasWide) return;
			wasWide = wide;
			previewColumn.Width = new GridLength(wide ? 1 : 0, GridUnitType.Star);
			regions.RowDefinitions[1].Height = new GridLength(wide ? 0 : 220);
			Grid.SetColumn(preview, wide ? 1 : 0); Grid.SetRow(preview, wide ? 0 : 1);
		};
		Grid.SetRow(regions, 1); document.Children.Add(regions);
		var footer = new Grid { ColumnSpacing = 16, Margin = new Thickness(0, 8, 0, 0) };
		footer.ColumnDefinitions.Add(new()); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
		footer.Children.Add(printSummary);
		var send = ActionButton("打印纸条…", Print); send.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
		footer.Children.Add(send); Grid.SetColumn(send, 1);
		Grid.SetRow(footer, 2); document.Children.Add(footer);
		void FitPaper()
		{
			if (paperScroll.ActualWidth > 32) paperScroll.ChangeView(null, null, (float)Math.Clamp((paperScroll.ActualWidth - 28) / 578, .15, 1));
		}
		void Command(string label, Symbol icon, Func<CancellationToken, Task> action)
		{
			var command = new AppBarButton { Label = label, Icon = new SymbolIcon(icon) };
			var key = icon switch { Symbol.Print => global::Windows.System.VirtualKey.P, Symbol.OpenFile => global::Windows.System.VirtualKey.O, _ => global::Windows.System.VirtualKey.S };
			command.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = key, Modifiers = global::Windows.System.VirtualKeyModifiers.Control });
			commands.PrimaryCommands.Add(command); BindAction(command, action);
		}
		void PresetCommand(string label, bool save)
		{
			var command = new AppBarButton { Label = label }; commands.SecondaryCommands.Add(command);
			BindAction(command, token => PresetFile(save, token));
		}
	}

	private void BuildFreeLayout()
	{
		var controls = new StackPanel { Spacing = 8 };
		controls.Children.Add(Paragraph("将文字、图片或二维码预览加入同一张纸；使用属性精调，或在纸上拖动。"));
		controls.Children.Add(pageHeight);
		controls.Children.Add(ActionButton("把当前内容加入纸面", async token =>
		{
			if (previewDirty) await RenderActiveAsync(token);
			if (layoutShown)
				throw new InvalidOperationException("请先单独预览要加入的文字、图片或二维码。");
			if (layers.Count >= 24)
				throw new InvalidOperationException("最多添加 24 个图层。");
			var snapshot = RequirePreview();
			int height = Math.Max(Value(pageHeight), Math.Max(snapshot.Height, layers.Select(item => item.Y + item.Height).DefaultIfEmpty().Max()));
			var layer = new PaperLayer(snapshot, 0, 0, 576, snapshot.Height, Source: CaptureLayerSource());
			pageHeight.Value = height;
			layers.Add(layer);
			UpdateLayerList(layers.Count - 1);
			await ShowLayout();
		}));
		AutomationProperties.SetName(layerList, "纸面图层，列表末尾为最上层");
		layerList.SelectionChanged += (_, _) =>
		{
			if (layerList.SelectedIndex < 0)
				return;
			var layer = layers[layerList.SelectedIndex];
			layerX.Value = layer.X;
			layerY.Value = layer.Y;
			layerWidth.Value = layer.Width;
			layerHeight.Value = layer.Height;
			try { LoadLayerEditor(layer); }
			catch (Exception error) { Report(error.Message, InfoBarSeverity.Error); }
		};
		controls.Children.Add(layerList);
		controls.Children.Add(ActionButton("显示当前排版", async _ =>
		{
			if (layers.Count == 0) throw new InvalidOperationException("请先添加排版对象。");
			bool pending = layoutPending;
			await ShowLayout(); layoutPending = pending;
		}));
		foreach (var field in new[] { layerX, layerY, layerWidth, layerHeight })
			controls.Children.Add(field);
		BuildLayerEditor(controls);
		controls.Children.Add(ActionButton("应用位置与尺寸", async _ =>
		{
			int index = SelectedLayer();
			var original = layers[index];
			var updated = original with { X = Value(layerX), Y = Value(layerY), Width = Value(layerWidth), Height = Value(layerHeight) };
			if (updated.Width != original.Width || updated.Height != original.Height)
				updated = RenderEditableLayer(updated);
			int height = Value(pageHeight);
			var result = PaperLayout.Render(height, layers.Select((layer, position) => position == index ? updated : layer));
			layers[index] = updated;
			await ShowLayout(result);
		}));
		controls.Children.Add(ActionButton("移到最上层", async _ =>
		{
			int index = SelectedLayer();
			var layer = layers[index];
			var composed = PaperLayout.Render(Value(pageHeight), layers.Where((_, position) => position != index).Append(layer));
			layers.RemoveAt(index);
			layers.Add(layer);
			UpdateLayerList(layers.Count - 1);
			await ShowLayout(composed);
		}));
		controls.Children.Add(ActionButton("移除所选图层", async _ =>
		{
			int index = SelectedLayer();
			var composed = PaperLayout.Render(Value(pageHeight), layers.Where((_, position) => position != index));
			layers.RemoveAt(index);
			UpdateLayerList(Math.Min(index, layers.Count - 1));
			await ShowLayout(composed);
		}));
		controls.Children.Add(ActionButton("显示合成纸面", _ => ShowLayout()));
		inspector.Children.Add(PropertyGroup("高级排版 · 自由图层", controls));
	}

	private int SelectedLayer() => layerList.SelectedIndex >= 0 ? layerList.SelectedIndex
	    : throw new InvalidOperationException("请先选择图层。");

	private void UpdateLayerList(int selected)
	{
		layerList.ItemsSource = layers.Select((_, index) => $"图层 {index + 1}").ToArray();
		layerList.SelectedIndex = selected;
	}

	private async Task ShowLayout(Raster? composed = null)
	{
		previewRevision++;
		previewTimer?.Stop();
		await Preview(composed ?? PaperLayout.Render(Value(pageHeight), layers), applyTrim: false);
		layoutShown = true;
		sourceMode = "layout";
		layoutPending = true;
	}

	private LayoutDraft? LayoutSnapshot() => layoutShown ? new(new List<PaperLayer>(layers)) : null;

	private async Task RestoreLayout(LayoutDraft layout, Raster saved)
	{
		if (layout.Layers.Count > 24)
			throw new InvalidDataException("保存的排版超过 24 个图层。");
		var rendered = PaperLayout.Render(saved.Height, layout.Layers);
		if (!rendered.Bytes.AsSpan().SequenceEqual(saved.Bytes))
			throw new InvalidDataException("保存的图层与点阵不一致。");
		layers.Clear();
		layers.AddRange(layout.Layers);
		pageHeight.Value = saved.Height;
		UpdateLayerList(layers.Count - 1);
		await ShowLayout(rendered);
		confirmedLayers = layers.ToArray(); confirmedHeight = saved.Height; layoutPending = false;
	}

	private void StartLayerDrag(object sender, PointerRoutedEventArgs args)
	{
		if (!layoutShown || operation != null || !args.GetCurrentPoint(paper).Properties.IsLeftButtonPressed)
			return;
		var point = args.GetCurrentPoint(paper).Position;
		int index = layers.FindLastIndex(layer => point.X >= layer.X && point.X < layer.X + layer.Width &&
		                                        point.Y >= layer.Y && point.Y < layer.Y + layer.Height);
		if (index < 0 || !paper.CapturePointer(args.Pointer))
			return;
		layerList.SelectedIndex = index;
		drag = (index, point.X, point.Y);
		args.Handled = true;
	}

	private async void EndLayerDrag(object sender, PointerRoutedEventArgs args)
	{
		if (drag is not { } start)
			return;
		drag = null;
		paper.ReleasePointerCapture(args.Pointer);
		args.Handled = true;
		if (operation != null || !layoutShown || start.Index >= layers.Count)
			return;
		using var active = new CancellationTokenSource();
		operation = active;
		try
		{
			var point = args.GetCurrentPoint(paper).Position;
			var layer = layers[start.Index];
			int height = Value(pageHeight);
			var updated = layer with
			{
				X = Math.Clamp(layer.X + (int)Math.Round(point.X - start.X), 0, 576 - layer.Width),
				Y = Math.Clamp(layer.Y + (int)Math.Round(point.Y - start.Y), 0, Math.Max(0, height - layer.Height))
			};
			var result = PaperLayout.Render(height, layers.Select((item, index) => index == start.Index ? updated : item));
			layers[start.Index] = updated;
			layerX.Value = updated.X;
			layerY.Value = updated.Y;
			await ShowLayout(result);
		}
		catch (Exception error) { Report(error.Message, InfoBarSeverity.Error); }
		finally { operation = null; }
	}
}
