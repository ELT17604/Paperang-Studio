using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Paperang.Core;
using Paperang.Windows;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow : Window
{
	private readonly ClientSession? trayHost;
	private ClientSession tray => trayHost ?? throw new InvalidOperationException("检查模式不访问打印机或中转。");
	private readonly TextBox text = new() { Header = "纸条正文", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140, MaxLength = 10000 };
	private readonly NumberBox font = Number("字号（像素）", 8, 96, 30);
	private readonly ComboBox fontFamily = Choice("打印字体", "Microsoft YaHei", "Segoe UI", "Arial", "Consolas");
	private readonly ComboBox alignment = Choice("对齐", "居中", "居左", "居右");
	private readonly CheckBox bold = new() { Content = "粗体" };
	private readonly NumberBox margin = Number("左右页边距（px）", 0, 120, 0);
	private readonly NumberBox marginY = Number("上下页边距（px）", 0, 200, 0);
	private readonly NumberBox spacing = Number("行间距（点）", 0, 48, 4);
	private readonly CheckBox trim = new() { Content = "预览时裁掉末尾空白行" };
	private readonly NumberBox threshold = Number("黑白阈值", 0, 255, 128);
	private readonly NumberBox density = Number("浓度", 0, 100, 100);
	private readonly NumberBox copies = Number("份数", 1, 20, 1);
	private readonly NumberBox before = Number("打印前送纸（行）", 0, 2000, 0);
	private readonly NumberBox feed = Number("批后送纸（行）", 0, 2000, P2Defaults.FinalFeed);
	private readonly NumberBox manual = Number("手动送纸（行）", 1, 2000, P2Defaults.FinalFeed);
	private readonly CheckBox between = new() { Content = "每份之间也执行批后送纸" };
	private readonly CheckBox keepHistory = new() { Content = "发送成功后将这份内容保存到本机历史" };
	private readonly TextBox materialName = new() { Header = "素材名称", MaxLength = 80 };
	private readonly ListView library = new() { Header = "本机已保存的素材与打印历史", SelectionMode = ListViewSelectionMode.Single,
		MinHeight = 160, MaxHeight = 400, HorizontalAlignment = HorizontalAlignment.Stretch };
	private readonly StackPanel relayConnection = new() { Spacing = 12 };
	private readonly TextBox qrText = new() { Header = "二维码内容", MaxLength = 1500 };
	private readonly NumberBox qrWidth = Number("二维码最大宽度（点）", 64, 576, 240);
	private readonly ComboBox qrCorrection = Choice("二维码纠错等级", "M", "L", "Q", "H");
	private readonly NumberBox blockGap = Number("排版块前留白（行）", 0, 200, 8);
	private readonly ListView blockList = new() { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 200 };
	private readonly List<(Raster Image, int Gap)> blocks = [];
	private readonly Image paper = new() { Stretch = Stretch.None };
	private readonly InfoBar status = new() { IsClosable = true };
	private readonly Grid document = new();
	private readonly StackPanel inspector = new() { Spacing = 12 };
	private readonly StackPanel printer = new() { Spacing = 12 };
	private readonly StackPanel relay = new() { Spacing = 12 };
	private readonly StackPanel archive = new() { Spacing = 12 };
	private readonly StackPanel output = new() { Spacing = 12, Padding = new Thickness(24) };
	private readonly Button cancel = new() { Content = "取消发送", IsEnabled = false };
	private CancellationTokenSource? operation;
	private Raster? raster;
	private TextDraft? previewDraft;
	private Action? openWorkspace;

	internal WorkspaceWindow(ClientSession? tray)
	{
		trayHost = tray;
		Title = "Paperang Studio · WinClient";
		var display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
		AppWindow.Resize(new global::Windows.Graphics.SizeInt32((int)(display.WorkArea.Width * .9), (int)(display.WorkArea.Height * .9)));
		SystemBackdrop = new MicaBackdrop();
		var navigation = new NavigationView { IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
		                                     IsSettingsVisible = false, CompactModeThresholdWidth = 700 };
		navigation.PaneTitle = "Paperang Studio";
		var navigationSurface = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 243, 245, 248));
		navigation.Background = navigationSurface;
		navigation.Resources["NavigationViewDefaultPaneBackground"] = navigationSurface;
		navigation.Resources["NavigationViewExpandedPaneBackground"] = navigationSurface;
		navigation.OpenPaneLength = 200;
		navigation.ExpandedModeThresholdWidth = 1200;
		navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Auto;
		var workspaceItem = new NavigationViewItem { Content = "打印工作台", Icon = new SymbolIcon(Symbol.Edit), Tag = "workspace" };
		var printerItem = new NavigationViewItem { Content = "打印机", Icon = new SymbolIcon(Symbol.Print), Tag = "printer" };
		var archiveItem = new NavigationViewItem { Content = "打印记录与素材", Icon = new SymbolIcon(Symbol.Library), Tag = "archive" };
		var relayItem = new NavigationViewItem { Content = "远程打印", Icon = new SymbolIcon(Symbol.World), Tag = "relay" };
		navigation.MenuItems.Add(workspaceItem);
		navigation.MenuItems.Add(printerItem);
		navigation.MenuItems.Add(archiveItem);
		navigation.MenuItems.Add(relayItem);
		navigation.MenuItems.Add(new NavigationViewItem { Content = "纸条模板", Icon = new SymbolIcon(Symbol.Document), Tag = "templates" });
		navigation.MenuItems.Add(new NavigationViewItem { Content = "工程查询", Icon = new SymbolIcon(Symbol.Setting), Tag = "engineering" });
		navigation.MenuItems.Add(new NavigationViewItem { Content = "访问与任务管理", Icon = new SymbolIcon(Symbol.Permissions), Tag = "administration" });
		navigation.MenuItems.Add(new NavigationViewItem { Content = "本地 API", Icon = new SymbolIcon(Symbol.Link), Tag = "api" });
		navigation.MenuItems.Add(new NavigationViewItem { Content = "纸带终端", Icon = new SymbolIcon(Symbol.Keyboard), Tag = "terminal" });
		navigation.MenuItems.Add(new NavigationViewItem { Content = "设置与关于", Icon = new SymbolIcon(Symbol.Help), Tag = "preferences" });
		var sessionControls = new StackPanel { Spacing = 10, Padding = new Thickness(12) };
		var allowPaper = new ToggleSwitch { Header = "全局允许耗纸", OnContent = "允许打印与送纸", OffContent = "暂停耗纸" };
		allowPaper.IsOn = trayHost?.AllowPaper ?? false;
		allowPaper.Toggled += (_, _) =>
		{
			try
			{
				trayHost?.SetPaperAllowed(allowPaper.IsOn);
				if (!allowPaper.IsOn) operation?.Cancel();
			}
			catch (Exception error) { Report(error.Message, InfoBarSeverity.Error); }
		};
		sessionControls.Children.Add(allowPaper);
		sessionControls.Children.Add(new TextBlock { Text = "v" + Program.Version, FontSize = 11 });
		var stopOperation = new Button { Content = "取消当前操作" };
		stopOperation.Click += (_, _) => { operation?.Cancel(); trayHost?.CancelCurrent(); };
		sessionControls.Children.Add(stopOperation);
		navigation.PaneFooter = sessionControls;
		openWorkspace = () => navigation.SelectedItem = workspaceItem;
		var page = new Grid();
		page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		var body = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch,
		                                VerticalContentAlignment = VerticalAlignment.Stretch };
		var scroll = new ScrollViewer { Content = output, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
		status.Margin = new Thickness(24, 8, 24, 0);
		Grid.SetRow(body, 1);
		page.Children.Add(status);
		page.Children.Add(body);
		navigation.Content = page;
		navigation.SelectionChanged += (_, selection) =>
		{
			if (selection.SelectedItem is NavigationViewItem { Tag: string key })
			{
				FrameworkElement panel = key switch { "printer" => printer, "archive" => archive, "relay" => relay, "templates" => templatePage, "engineering" => engineeringPage, "administration" => administrationPage, "api" => apiPage, "terminal" => terminalPage, "preferences" => preferencesPage, _ => document };
				output.Children.Clear();
				body.Content = panel == document ? document : scroll;
				if (panel != document)
					output.Children.Add(panel);
				if (panel == archive)
				{
					try { RefreshLibrary(); }
					catch (Exception error) { Report(error.Message, InfoBarSeverity.Error); }
				}
			}
		};
		Content = navigation;
		BuildWorkspace();
		BuildPrinter();
		BuildRelay();
		BuildLibrary();
		BuildTemplates();
		BuildEngineering();
		BuildAdministration();
		BuildLocalApi();
		BuildTerminal();
		BuildPreferences();
		InitializeEditing();
		InitializeDrop();
		foreach (var panel in new[] { printer, relay, archive, templatePage, engineeringPage, administrationPage, apiPage, terminalPage, preferencesPage })
		{
			panel.MaxWidth = 820;
			panel.HorizontalAlignment = HorizontalAlignment.Center;
		}
		body.Content = document;
		cancel.Click += (_, _) => operation?.Cancel();
		void RefreshSession() => DispatcherQueue.TryEnqueue(() =>
		{
			ExpireAdministration();
			RefreshPrinterConnection();
			bool paperAllowed = trayHost?.AllowPaper ?? false;
			if (allowPaper.IsOn != paperAllowed) allowPaper.IsOn = paperAllowed;
			connectionStatus.Text = trayHost?.DeviceStatus ?? "未连接打印机";
			RefreshPrintSummary();
			relayStatus.Text = trayHost?.RelayStatus ?? "尚未连接中转服务器";
			deviceProgress.Value = Math.Clamp(trayHost?.Progress ?? 0, 0, 1) * 100;
			deviceProgress.Visibility = trayHost?.Working == true ? Visibility.Visible : Visibility.Collapsed;
			sessionJobs.ItemsSource = trayHost?.Jobs ?? [];
		});
		var statusTimer = DispatcherQueue.CreateTimer();
		statusTimer.Interval = TimeSpan.FromMilliseconds(500);
		statusTimer.Tick += (_, _) => RefreshSession();
		statusTimer.Start();
		if (trayHost != null)
			trayHost.Changed += RefreshSession;
		Closed += (_, _) =>
		{
			operation?.Cancel();
			statusTimer.Stop();
			if (trayHost != null) trayHost.Changed -= RefreshSession;
		};
	}

	private void BuildWorkspace()
	{
		var textPanel = new StackPanel { Spacing = 8 };
		textPanel.Children.Add(text);
		textPanel.Children.Add(TwoColumns(fontFamily, font));
		textPanel.Children.Add(TwoColumns(alignment, bold));
		textPanel.Children.Add(TwoColumns(spacing, contentWidth));
		textPanel.Children.Add(TwoColumns(margin, marginY));
		inspector.Children.Add(PropertyGroup("文字", textPanel, true));
		var imagePanel = new StackPanel { Spacing = 8 };
		imagePanel.Children.Add(imageName);
		imagePanel.Children.Add(ActionButton("选择图片…", Import));
		foreach (var field in new Microsoft.UI.Xaml.FrameworkElement[] { imageFit, rotation, cropHeight })
			imagePanel.Children.Add(field);
		inspector.Children.Add(PropertyGroup("图片", imagePanel));
		var rasterPanel = new StackPanel { Spacing = 8 };
		rasterPanel.Children.Add(conversion);
		rasterPanel.Children.Add(threshold);
		rasterPanel.Children.Add(brightness);
		rasterPanel.Children.Add(TwoColumns(contrast, gamma));
		rasterPanel.Children.Add(invert);
		rasterPanel.Children.Add(trim);
		inspector.Children.Add(PropertyGroup("黑白处理", rasterPanel));
		var qrPanel = new StackPanel { Spacing = 8 };
		qrPanel.Children.Add(qrFormat);
		qrPanel.Children.Add(qrText);
		qrPanel.Children.Add(qrWidth);
		qrPanel.Children.Add(qrCorrection);
		qrPanel.Children.Add(qrShape);
		qrPanel.Children.Add(qrBorder);
		inspector.Children.Add(PropertyGroup("二维码", qrPanel));
		BuildFreeLayout();
		var layout = new StackPanel { Spacing = 8 };
		AutomationProperties.SetName(blockList, "纵向排版块");
		layout.Children.Add(blockGap);
		layout.Children.Add(SimpleAction("把当前预览添加为排版块", () =>
		{
			if (blocks.Count >= 24)
				throw new InvalidOperationException("最多添加 24 个排版块。");
			blocks.Add((RequirePreview(), blocks.Count == 0 ? 0 : Value(blockGap)));
			RefreshBlocks(blocks.Count - 1);
		}));
		layout.Children.Add(blockList);
		layout.Children.Add(SimpleAction("上移所选块", () => MoveBlock(-1)));
		layout.Children.Add(SimpleAction("下移所选块", () => MoveBlock(1)));
		layout.Children.Add(SimpleAction("移除所选块", () =>
		{
			int index = SelectedBlock();
			blocks.RemoveAt(index);
			RefreshBlocks(Math.Min(index, blocks.Count - 1));
		}));
		layout.Children.Add(ActionButton("生成组合预览", async _ =>
		{
			importedRaster = Raster.Stack(blocks); sourceMode = "raster";
			await Preview(importedRaster, applyTrim: false);
		}));
		inspector.Children.Add(PropertyGroup("纵向组合排版", layout));
		inspector.Children.Add(keepHistory);
		BuildDesk();
	}

	private void RefreshBlocks(int selected)
	{
		blockList.ItemsSource = blocks.Select((block, index) => $"{index + 1}. {block.Image.Height} 行 · 前留白 {block.Gap} 行").ToArray();
		blockList.SelectedIndex = selected;
	}

	private int SelectedBlock() => blockList.SelectedIndex >= 0 ? blockList.SelectedIndex : throw new InvalidOperationException("请先选择一个排版块。");

	private void MoveBlock(int offset)
	{
		int index = SelectedBlock(), next = index + offset;
		if (next < 0 || next >= blocks.Count)
			return;
		(blocks[index], blocks[next]) = (blocks[next], blocks[index]);
		RefreshBlocks(next);
	}

	private void BuildLibrary()
	{
		archive.Children.Add(Heading("素材与历史"));
		archive.Children.Add(sessionJobs);
		archive.Children.Add(Paragraph("只有手动保存的素材和主动选择留存的打印内容才会保存在本机。各最多保留最近 10 项，使用当前 Windows 用户加密。"));
		archive.Children.Add(materialName);
		archive.Children.Add(ActionButton("保存当前预览为素材", _ =>
		{
			WorkspaceLibrary.Add(string.IsNullOrWhiteSpace(materialName.Text) ? "未命名" : materialName.Text.Trim(),
			                     RequirePreview(), CurrentOptions(), previewDraft, true, LayoutSnapshot());
			RefreshLibrary();
			Report("素材已保存。", InfoBarSeverity.Success);
			return Task.CompletedTask;
		}));
		archive.Children.Add(library);
		archive.Children.Add(ActionButton("载入所选项到预览", async _ =>
		{
			var entry = SelectedEntry();
			if (entry.Options.TransferSettings is { } savedSettings) ApplyWorkspaceSettings(savedSettings);
			density.Value = entry.Options.Density;
			copies.Value = entry.Options.Copies;
			before.Value = entry.Options.Before;
			feed.Value = entry.Options.After;
			between.IsChecked = entry.Options.BetweenCopies;
			if (entry.Draft is { } draft)
			{
				text.Text = draft.Text;
				if (entry.Options.TransferSettings == null)
				{
				fontFamily.SelectedItem = draft.Font;
				font.Value = draft.Size;
				bold.IsChecked = draft.Bold;
				alignment.SelectedItem = draft.Alignment;
				margin.Value = draft.Margin;
				marginY.Value = draft.MarginY;
				spacing.Value = draft.Spacing;
				}
			}
			if (entry.Layout is { } layout)
				await RestoreLayout(layout, entry.Raster.Validate());
			else
			{
				sourceMode = entry.Draft == null ? "raster" : "text";
				importedRaster = entry.Raster;
				await Preview(entry.Raster.Validate(), entry.Draft, applyTrim: false);
			}
			openWorkspace?.Invoke();
		}));
		archive.Children.Add(ActionButton("删除所选项…", async _ =>
		{
			var entry = SelectedEntry();
			var dialog = new ContentDialog { Title = "删除保存的内容？", Content = entry.ToString(), XamlRoot = archive.XamlRoot,
			                                PrimaryButtonText = "删除", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
			if (await dialog.ShowAsync() != ContentDialogResult.Primary)
				return;
			WorkspaceLibrary.Remove(entry.Id);
			RefreshLibrary();
		}));
	}

	private void RefreshLibrary() => library.ItemsSource = WorkspaceLibrary.Read();
	private WorkspaceEntry SelectedEntry() => library.SelectedItem as WorkspaceEntry ?? throw new InvalidOperationException("请先选择一个保存项。");
	private LocalPrintOptions CurrentOptions() => new(Value(density), Value(copies), Value(before), Value(feed), between.IsChecked == true, ReadWorkspaceSettings());

	private void BuildPrinter()
	{
		printer.Children.Add(Heading("打印机"));
		printer.Children.Add(Paragraph("选择 USB 或蓝牙连接。扫描时可以继续选择设备和编辑纸条。"));
		BuildConnectionSettings();
		printer.Children.Add(ActionButton("刷新电量与纸仓", async token =>
		{
			if (!tray.Connected) throw new InvalidOperationException("请先连接打印机。");
			Report(await tray.UseLocalPrinterAsync(null, new(), token));
		}));
		printer.Children.Add(ActionButton("断开打印机", async _ =>
		{
			await tray.DisconnectAsync();
			Report("打印机已断开。");
		}));
		printer.Children.Add(ActionButton("仅应用浓度并回读", async token =>
		{
			if (!tray.Connected) throw new InvalidOperationException("请先连接打印机。");
			int value = Value(density);
			await tray.UseLocalPrinterAsync(null, new(), token, densityOnly: value);
			Report($"浓度 {value} 已设置并回读确认。", InfoBarSeverity.Success);
		}));
		printer.Children.Add(manual);
		printer.Children.Add(ActionButton("手动送纸", async token =>
		{
			if (!tray.Connected) throw new InvalidOperationException("请先连接打印机。");
			int lines = Value(manual);
			await tray.UseLocalPrinterAsync(null, new(), token, lines);
			Report($"已确认送纸 {lines} 行指令，请核对纸面。", InfoBarSeverity.Success);
		}));
	}

	private void BuildRelay()
	{
		relay.Children.Add(Heading("远程打印"));
		relay.Children.Add(Paragraph("连接中转服务器后，在这台电脑接收和打印远程纸条。"));
		relay.Children.Add(relayStatus);
		relay.Children.Add(ActionButton("查看中转状态", async _ => Report(await tray.RelayStatusAsync())));
		relay.Children.Add(ActionButton("启动中转监听", async _ => Report(await tray.SetRelayAsync(true))));
		relay.Children.Add(ActionButton("断开中转", async _ => Report(await tray.SetRelayAsync(false))));
		var serverSettings = (Expander)CollapsibleGroup("服务器连接与配对", relayConnection);
		relay.Children.Add(SimpleAction("设置服务器与配对私钥…", () => serverSettings.IsExpanded = true));
		relay.Children.Add(serverSettings);
		BuildRelayPreferences();
		BuildRelayQueue();
	}

	private async Task Import(CancellationToken token)
	{
		var picker = new FileOpenPicker();
		InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
		foreach (string extension in new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".raw", ".json" })
			picker.FileTypeFilter.Add(extension);
		var file = await picker.PickSingleFileAsync();
		if (file == null)
			return;
		await ImportPath(file.Path, file.Name, token);
	}
	private async Task ImportPath(string path, string name, CancellationToken token)
	{
		int importRevision = ++previewRevision;
		previewTimer?.Stop();
		if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
		{
			var imported = await Task.Run(() => WorkspaceJson.Read(path), token);
			token.ThrowIfCancellationRequested();
			if (previewRevision != importRevision) throw new OperationCanceledException("内容已更改，已取消旧导入。");
			if (imported.Settings is { } preset)
			{
				ApplyWorkspaceSettings(preset);
				if (HasActiveContent()) await RenderActiveAsync(token);
				Report($"已导入参数预设 · 字体 {fontFamily.SelectedItem} · 边距与打印设置已更新。", InfoBarSeverity.Success);
				return;
			}
			importedRaster = imported.Raster!; sourceMode = "raster";
			importedContent.Text = $"{name}\n576 × {importedRaster.Height} 点";
			selectEditorGroup?.Invoke("导入点阵");
			await Preview(importedRaster, applyTrim: false, expectedRevision: importRevision);
			Report("点阵已导入，可直接打印或加入排版。", InfoBarSeverity.Success);
			return;
		}
		var settings = ReadWorkspaceSettings();
		var next = await Task.Run(() => WorkspaceFiles.Open(path, settings), token);
		token.ThrowIfCancellationRequested();
		if (previewRevision != importRevision) throw new OperationCanceledException("内容已更改，已取消旧导入。");
		if (Path.GetExtension(path).ToLowerInvariant() is ".raw" or ".json")
		{
			importedRaster = next;
			sourceMode = "raster";
			importedContent.Text = $"{name}\n576 × {next.Height} 点";
			selectEditorGroup?.Invoke("导入点阵");
		}
		else
		{
			sourceImagePath = path;
			imageName.Text = name;
			sourceMode = "image";
			selectEditorGroup?.Invoke("图片");
		}
		await Preview(next, applyTrim: sourceMode == "image", expectedRevision: importRevision);
	}

	private async Task Export(CancellationToken token)
	{
		if (previewDirty) await RenderActiveAsync(token);
		var snapshot = RequirePreview();
		var picker = new FileSavePicker { SuggestedFileName = "Paperang" };
		InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
		picker.FileTypeChoices.Add("PNG", new List<string> { ".png" });
		picker.FileTypeChoices.Add("RAW", new List<string> { ".raw" });
		picker.FileTypeChoices.Add("raster1 JSON", new List<string> { ".json" });
		var file = await picker.PickSaveFileAsync();
		if (file == null)
			return;
		await Task.Run(() => WorkspaceFiles.Export(file.Path, snapshot), token);
		Report("已导出当前预览。", InfoBarSeverity.Success);
	}

	private async Task Print(CancellationToken token)
	{
		if (previewDirty) await RenderActiveAsync(token);
		var snapshot = RequirePreview();
		if (!tray.AllowPaper) throw new InvalidOperationException("耗纸已暂停，请先开启左侧“全局允许耗纸”。");
		var options = CurrentOptions();
		bool saveHistory = keepHistory.IsChecked == true;
		var dialog = new ContentDialog { Title = "打印这份预览？", XamlRoot = document.XamlRoot,
		                                Content = $"位图 {snapshot.Height} 行 × {options.Copies} 份，浓度 {options.Density}。\n" +
		                                          $"前送纸 {options.Before} 行，后送纸 {options.After} 行" +
		                                          (options.BetweenCopies ? "（每份）" : "（整批末尾）"),
		                                PrimaryButtonText = "发送到 P2", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
		if (await dialog.ShowAsync() != ContentDialogResult.Primary)
			return;
		cancel.IsEnabled = true;
		Report("正在连接或发送到 P2…");
		await tray.UseLocalPrinterAsync(snapshot, options, token);
		if (saveHistory)
		{
			try { WorkspaceLibrary.Add($"{snapshot.Height} 行 · {options.Copies} 份", snapshot, options, previewDraft, false, LayoutSnapshot()); }
			catch (Exception) { Report("数据已发送，但本机历史保存失败。请核对纸面，不要直接重试发送。", InfoBarSeverity.Warning); return; }
		}
		Report("数据发送完成，请核对纸面。", InfoBarSeverity.Success);
	}

	private async Task Preview(Raster next, TextDraft? draft = null, bool applyTrim = true, int? expectedRevision = null)
	{
		if (applyTrim && trim.IsChecked == true)
			next = next.TrimTrailingBlankRows();
		using var bitmap = NoteRenderer.Preview(next);
		using var bytes = new MemoryStream();
		bitmap.Save(bytes, System.Drawing.Imaging.ImageFormat.Png);
		bytes.Position = 0;
		using var stream = bytes.AsRandomAccessStream();
		var image = new BitmapImage();
		await image.SetSourceAsync(stream);
		if (expectedRevision.HasValue && expectedRevision != previewRevision) return;
		paper.Source = image;
		paper.Width = 576;
		paper.Height = next.Height;
		previewSheet.MinHeight = next.Height;
		layoutShown = false;
		raster = next;
		RefreshPrintSummary();
		previewDraft = draft;
		previewDirty = false;
		paperHint.Text = $"纸宽 576 点 · {next.Height} 行";
		status.IsOpen = false;
	}

	private Raster RequirePreview()
	{
		if (sourceMode == "layout" && layoutPending) throw new InvalidOperationException("请先在高级排版中确认并渲染本次更改。");
		return !previewDirty && raster != null ? raster : throw new InvalidOperationException("预览尚未更新，请等待渲染完成或修正参数。");
	}
	private void Report(string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
	{
		status.Message = message;
		status.Severity = severity;
		status.IsOpen = true;
	}
	private Button ActionButton(string label, Func<CancellationToken, Task> action)
	{
		var button = new Button { Content = label };
		BindAction(button, action);
		return button;
	}
	private void BindAction(Microsoft.UI.Xaml.Controls.Primitives.ButtonBase button, Func<CancellationToken, Task> action)
	{
		button.Click += async (_, _) =>
		{
			if (operation != null)
			{
				Report("当前操作尚未结束，请稍候或取消后继续。");
				return;
			}
			using var active = new CancellationTokenSource();
			operation = active;
			cancel.IsEnabled = true;
			var disabledControls = new List<(Control Control, bool Enabled)>();
			foreach (var panel in new[] { inspector, printer, relay, archive, templatePage, engineeringPage, administrationPage, apiPage, terminalPage, preferencesPage })
				Disable(panel);
			button.IsEnabled = false;
			try { await action(active.Token); }
			catch (OperationCanceledException) { Report("已取消；如果已经开始发送，请检查纸面再决定是否重试。", InfoBarSeverity.Warning); }
			catch (Exception error) { Report(error.Message, InfoBarSeverity.Error); }
			finally
			{
				operation = null; button.IsEnabled = true; cancel.IsEnabled = false;
				foreach (var (control, enabled) in disabledControls) control.IsEnabled = enabled;
				apiPort.IsEnabled = apiToken.IsEnabled = apiOrigins.IsEnabled = apiServer == null;
				if (previewDirty) RequestPreview();
			}
			void Disable(DependencyObject parent)
			{
				if (parent is Control control)
				{
					disabledControls.Add((control, control.IsEnabled));
					control.IsEnabled = false;
					return;
				}
				for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
					Disable(VisualTreeHelper.GetChild(parent, index));
			}
		};
	}
	private Button SimpleAction(string label, Action action) => ActionButton(label, _ =>
	{
		action();
		return Task.CompletedTask;
	});
	private static TextBlock Heading(string title) => new() { Text = title, FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
	private static FrameworkElement PropertyGroup(string title, FrameworkElement content, bool expanded = false)
	{
		var panel = new StackPanel { Spacing = 12 };
		panel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
		panel.Children.Add(content);
		return new Border { Tag = title, Child = panel, Padding = new Thickness(16), CornerRadius = new CornerRadius(8),
			Background = ThemeBrush("CardBackgroundFillColorDefaultBrush"), BorderBrush = ThemeBrush("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1) };
	}
	private static FrameworkElement CollapsibleGroup(string title, FrameworkElement content) =>
		new Expander { Header = title, Content = content, IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch,
			HorizontalContentAlignment = HorizontalAlignment.Stretch };
	private static Microsoft.UI.Xaml.Media.Brush ThemeBrush(string name) =>
		(Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[name];
	private static Grid TwoColumns(FrameworkElement first, FrameworkElement second)
	{
		var grid = new Grid { ColumnSpacing = 12 };
		grid.ColumnDefinitions.Add(new());
		grid.ColumnDefinitions.Add(new());
		first.HorizontalAlignment = HorizontalAlignment.Stretch;
		second.HorizontalAlignment = HorizontalAlignment.Stretch;
		second.VerticalAlignment = VerticalAlignment.Bottom;
		grid.Children.Add(first);
		grid.Children.Add(second);
		Grid.SetColumn(second, 1);
		return grid;
	}
	private static TextBlock Paragraph(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
	private static ComboBox Choice(string label, params string[] options) => new() { Header = label, ItemsSource = options, SelectedIndex = 0 };
	private static NumberBox Number(string label, int minimum, int maximum, int value) =>
	    new() { Header = label, Minimum = minimum, Maximum = maximum, Value = value, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
	private static int Value(NumberBox control)
	{
		if (!double.IsFinite(control.Value) || control.Value != Math.Truncate(control.Value) ||
		    control.Value < control.Minimum || control.Value > control.Maximum)
			throw new InvalidDataException($"{control.Header}须为 {control.Minimum}–{control.Maximum} 的整数。");
		return (int)control.Value;
	}
}
