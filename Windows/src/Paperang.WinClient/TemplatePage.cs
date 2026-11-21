using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Paperang.Core;
using Paperang.Windows;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private readonly StackPanel templatePage = new() { Spacing = 12 };
	private readonly ComboBox templates = new() { Header = "模板", HorizontalAlignment = HorizontalAlignment.Stretch };
	private readonly ListView templateBlocks = new() { Header = "区块顺序", MaxHeight = 220 };
	private readonly TextBox templateName = new() { Header = "模板名称", MaxLength = 80 };
	private readonly ComboBox blockKind = Choice("区块内容", RelayBlock.Kinds);
	private readonly TextBox blockText = new() { Header = "标题 / 自定义文字", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 2000, Height = 90 };
	private readonly CheckBox blockEnabled = new() { Content = "启用区块", IsChecked = true };
	private readonly CheckBox blockBold = new() { Content = "粗体" };
	private readonly ComboBox blockFont = Choice("字体", "PingFang SC", "Microsoft YaHei", "Menlo", "Consolas", "Helvetica", "Arial");
	private readonly ComboBox blockAlignment = Choice("对齐", "居左", "居中", "居右");
	private readonly NumberBox blockSize = Number("字号", 8, 96, 24);
	private readonly NumberBox blockMargin = Number("左右边距", 0, 120, 0);
	private readonly NumberBox templateGap = Number("区块前留白", 0, 200, 8);
	private readonly Image templatePreview = new() { Stretch = Stretch.Uniform, MaxHeight = 420 };
	private List<RelayTemplate> templateDrafts = [];

	private RelayTemplate CurrentTemplate() => templates.SelectedItem as RelayTemplate
		?? throw new InvalidOperationException("请先选择一个模板。");

	private void BuildTemplates()
	{
		templatePage.Children.Add(Heading("纸条模板"));
		templatePage.Children.Add(Paragraph("编辑远程纸条的区块、样式和顺序。只有“保存并应用”才会改变接收器配置。"));
		templatePage.Children.Add(ActionButton("载入已保存模板 / 放弃草稿", _ =>
		{
			var saved = tray.Settings();
			templateDrafts = saved.Templates.Select(item => item with { Blocks = item.Blocks.ToList() }).ToList();
			RefreshTemplates(saved.TemplateId);
			return Task.CompletedTask;
		}));
		templates.SelectionChanged += (_, _) =>
		{
			if (templates.SelectedItem is not RelayTemplate selected) return;
			templateName.Text = selected.Name;
			RefreshTemplateBlocks(0);
		};
		templateBlocks.SelectionChanged += (_, _) =>
		{
			if (templateBlocks.SelectedItem is not RelayBlock block) return;
			blockKind.SelectedItem = block.Kind;
			blockText.Text = block.Text;
			blockEnabled.IsChecked = block.Enabled;
			blockBold.IsChecked = block.Bold;
			if (!blockFont.Items.Contains(block.Font))
				blockFont.ItemsSource = blockFont.Items.Cast<string>().Append(block.Font).ToArray();
			blockFont.SelectedItem = block.Font;
			blockAlignment.SelectedItem = block.Alignment;
			blockSize.Value = block.Size;
			blockMargin.Value = block.Margin;
			templateGap.Value = block.Gap;
		};
		templatePage.Children.Add(templates);
		templatePage.Children.Add(templateName);
		var templateActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		templateActions.Children.Add(SimpleAction("复制模板", () =>
		{
			if (templateDrafts.Count >= 30) throw new InvalidOperationException("最多保存 30 个模板。");
			var current = CurrentTemplate();
			var copy = current with { Id = Guid.NewGuid().ToString(), Name = (current.Name + " 副本")[..Math.Min(80, current.Name.Length + 3)],
				Blocks = current.Blocks.Select(block => block with { Id = Guid.NewGuid().ToString() }).ToList() };
			templateDrafts.Add(copy);
			RefreshTemplates(copy.Id);
		}));
		templateActions.Children.Add(ActionButton("删除模板…", async _ =>
		{
			var current = CurrentTemplate();
			if (templateDrafts.Count <= 1) throw new InvalidOperationException("至少保留一个模板。");
			if (!await Confirm(templatePage, "删除模板草稿？", current.Name)) return;
			templateDrafts.Remove(current);
			RefreshTemplates(templateDrafts[0].Id);
		}));
		templatePage.Children.Add(templateActions);
		templatePage.Children.Add(templateBlocks);
		templatePage.Children.Add(TwoColumns(blockKind, blockFont));
		templatePage.Children.Add(blockText);
		templatePage.Children.Add(TwoColumns(blockEnabled, blockBold));
		templatePage.Children.Add(TwoColumns(blockAlignment, blockSize));
		templatePage.Children.Add(TwoColumns(blockMargin, templateGap));
		templatePage.Children.Add(SimpleAction("应用区块属性", ApplyTemplateBlock));
		var blockActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		blockActions.Children.Add(SimpleAction("添加区块", () =>
		{
			var current = CurrentTemplate();
			if (current.Blocks.Count >= 24) throw new InvalidOperationException("每个模板最多 24 个区块。");
			current.Blocks.Add(new() { Kind = (string)blockKind.SelectedItem });
			RefreshTemplateBlocks(current.Blocks.Count - 1);
		}));
		blockActions.Children.Add(SimpleAction("上移", () => MoveTemplateBlock(-1)));
		blockActions.Children.Add(SimpleAction("下移", () => MoveTemplateBlock(1)));
		blockActions.Children.Add(ActionButton("删除区块…", async _ =>
		{
			var current = CurrentTemplate();
			if (templateBlocks.SelectedIndex < 0) throw new InvalidOperationException("请先选择区块。");
			if (current.Blocks.Count <= 1) throw new InvalidOperationException("至少保留一个区块。");
			if (!await Confirm(templatePage, "删除所选区块？", templateBlocks.SelectedItem.ToString()!)) return;
			int index = templateBlocks.SelectedIndex;
			current.Blocks.RemoveAt(index);
			RefreshTemplateBlocks(Math.Min(index, current.Blocks.Count - 1));
		}));
		templatePage.Children.Add(blockActions);
		var preset = Choice("内置模板", "便签", "小票", "复古电传", "访客纪念");
		templatePage.Children.Add(preset);
		templatePage.Children.Add(SimpleAction("从内置样式新建", () =>
		{
			if (templateDrafts.Count >= 30) throw new InvalidOperationException("最多保存 30 个模板。");
			var added = RelayTemplate.Preset((string)preset.SelectedItem);
			templateDrafts.Add(added);
			RefreshTemplates(added.Id);
		}));
		templatePage.Children.Add(ActionButton("导入模板 JSON…", token => TemplateFile(false, token)));
		templatePage.Children.Add(ActionButton("导出模板 JSON…", token => TemplateFile(true, token)));
		templatePage.Children.Add(ActionButton("生成示例预览", async token =>
		{
			ApplyTemplateBlock();
			var selected = CurrentTemplate();
			var sample = new RelayJob { Id = Guid.NewGuid().ToString(), Text = "纸条模板预览\nPaperang Studio", Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
			var settings = new AppSettings { Style = "自定义", Templates = [selected], TemplateId = selected.Id };
			var next = await Task.Run(() => new NoteRenderer().Render(sample, settings), token);
			await DisplayRaster(templatePreview, next);
		}));
		templatePage.Children.Add(templatePreview);
		templatePage.Children.Add(SimpleAction("保存并应用", () =>
		{
			ApplyTemplateBlock();
			foreach (var item in templateDrafts) item.Validate();
			var settings = tray.Settings() with { Style = "自定义", Templates = templateDrafts.Select(item => item with { Blocks = item.Blocks.ToList() }).ToList(), TemplateId = CurrentTemplate().Id };
			tray.SaveSettings(settings);
			Report("模板已保存并应用。", InfoBarSeverity.Success);
		}));
		templateDrafts = RelayTemplate.Defaults();
		RefreshTemplates(templateDrafts[0].Id);
	}

	private void RefreshTemplates(string id)
	{
		templates.ItemsSource = templateDrafts.ToArray();
		templates.SelectedItem = templateDrafts.FirstOrDefault(item => item.Id == id) ?? templateDrafts[0];
	}
	private void RefreshTemplateBlocks(int selected)
	{
		templateBlocks.ItemsSource = CurrentTemplate().Blocks.ToArray();
		templateBlocks.SelectedIndex = selected;
	}
	private void ApplyTemplateBlock()
	{
		var current = CurrentTemplate();
		if (templateBlocks.SelectedIndex < 0) throw new InvalidOperationException("请先选择区块。");
		int index = templateBlocks.SelectedIndex;
		var updated = current.Blocks[index] with { Kind = (string)blockKind.SelectedItem, Text = blockText.Text,
			Font = (string)blockFont.SelectedItem, Enabled = blockEnabled.IsChecked == true, Bold = blockBold.IsChecked == true,
			Alignment = (string)blockAlignment.SelectedItem, Size = blockSize.Value, Margin = Value(blockMargin), Gap = Value(templateGap) };
		var candidate = current with { Name = templateName.Text.Trim(), Blocks = current.Blocks.Select((block, position) => position == index ? updated : block).ToList() };
		candidate.Validate();
		templateDrafts[templateDrafts.IndexOf(current)] = candidate;
		RefreshTemplates(candidate.Id);
		RefreshTemplateBlocks(index);
	}
	private void MoveTemplateBlock(int delta)
	{
		var current = CurrentTemplate();
		int index = templateBlocks.SelectedIndex, next = index + delta;
		if (index < 0) throw new InvalidOperationException("请先选择区块。");
		if (next < 0 || next >= current.Blocks.Count) return;
		(current.Blocks[index], current.Blocks[next]) = (current.Blocks[next], current.Blocks[index]);
		RefreshTemplateBlocks(next);
	}
	private async Task TemplateFile(bool save, CancellationToken token)
	{
		if (save)
		{
			ApplyTemplateBlock();
			var picker = new FileSavePicker { SuggestedFileName = "Paperang-template" };
			picker.FileTypeChoices.Add("模板 JSON", new List<string> { ".json" });
			InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
			var file = await picker.PickSaveFileAsync();
			if (file != null) await File.WriteAllTextAsync(file.Path, JsonSerializer.Serialize(CurrentTemplate(), RelayClient.Json), token);
			return;
		}
		if (templateDrafts.Count >= 30) throw new InvalidOperationException("最多保存 30 个模板。");
		var open = new FileOpenPicker();
		open.FileTypeFilter.Add(".json");
		InitializeWithWindow.Initialize(open, WindowNative.GetWindowHandle(this));
		var input = await open.PickSingleFileAsync();
		if (input == null) return;
		if (new FileInfo(input.Path).Length > 128_000) throw new InvalidDataException("模板文件过大。");
		var imported = JsonSerializer.Deserialize<RelayTemplate>(await File.ReadAllTextAsync(input.Path, token), RelayClient.Json)
			?? throw new InvalidDataException("模板文件为空。");
		imported.Validate();
		imported = imported with { Id = Guid.NewGuid().ToString(), Blocks = imported.Blocks.Select(block => block with { Id = Guid.NewGuid().ToString() }).ToList() };
		templateDrafts.Add(imported);
		RefreshTemplates(imported.Id);
	}
	private static async Task<bool> Confirm(FrameworkElement parent, string title, string message) =>
		await new ContentDialog { Title = title, Content = message, XamlRoot = parent.XamlRoot,
			PrimaryButtonText = "确认", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close }.ShowAsync() == ContentDialogResult.Primary;
	private static async Task DisplayRaster(Image target, Raster value)
	{
		using var bitmap = NoteRenderer.Preview(value);
		using var bytes = new MemoryStream();
		bitmap.Save(bytes, System.Drawing.Imaging.ImageFormat.Png);
		bytes.Position = 0;
		using var stream = bytes.AsRandomAccessStream();
		var image = new BitmapImage();
		await image.SetSourceAsync(stream);
		target.Source = image;
	}
}
