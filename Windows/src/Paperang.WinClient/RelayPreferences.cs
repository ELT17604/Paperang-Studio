using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Paperang.Core;
using Paperang.Windows;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private void BuildRelayPreferences()
	{
		var panel = new StackPanel { Spacing = 10 };
		var style = Choice("纸条样式", "便签", "小票", "复古电传", "访客纪念", "自定义");
		var template = new ComboBox { Header = "自定义模板" };
		var concentration = Number("远程打印浓度", 0, 100, 100);
		var trailing = Number("远程打印后送纸（行）", 0, 2000, 160);
		var automatically = new CheckBox { Content = "开启全局耗纸后自动打印队列" };
		var flags = new[] { "正文", "投稿身份", "时间", "短编号", "IP 地址", "User Agent", "浏览器信息" }
			.Select(label => new CheckBox { Content = label }).ToArray();
		void Load()
		{
			var saved = trayHost?.Settings() ?? new();
			style.SelectedItem = saved.Style; concentration.Value = saved.Density; trailing.Value = saved.Feed;
			automatically.IsChecked = saved.AutoAllowPrint;
			template.ItemsSource = saved.Templates; template.SelectedItem = saved.Templates.FirstOrDefault(item => item.Id == saved.TemplateId) ?? saved.Templates[0];
			bool[] values = [saved.IncludeText, saved.IncludeSender, saved.IncludeTime, saved.IncludeId, saved.IncludeIp, saved.IncludeUa, saved.IncludeDevice];
			for (int index = 0; index < flags.Length; index++) flags[index].IsChecked = values[index];
		}
		AppSettings Draft() => tray.Settings() with { Style = (string)style.SelectedItem, Density = Value(concentration), Feed = Value(trailing),
			TemplateId = (template.SelectedItem as RelayTemplate)?.Id ?? "", AutoAllowPrint = automatically.IsChecked == true,
			IncludeText = flags[0].IsChecked == true, IncludeSender = flags[1].IsChecked == true, IncludeTime = flags[2].IsChecked == true,
			IncludeId = flags[3].IsChecked == true, IncludeIp = flags[4].IsChecked == true, IncludeUa = flags[5].IsChecked == true,
			IncludeDevice = flags[6].IsChecked == true };
		try { Load(); } catch (Exception error) { Report(error.Message, InfoBarSeverity.Error); }
		panel.Children.Add(style); panel.Children.Add(template);
		panel.Children.Add(TwoColumns(concentration, trailing));
		panel.Children.Add(Paragraph("纸面附加信息（自定义模板由其区块决定）"));
		var flagPanel = new StackPanel { Spacing = 6 };
		foreach (var flag in flags) flagPanel.Children.Add(flag);
		panel.Children.Add(flagPanel); panel.Children.Add(automatically);
		panel.Children.Add(SimpleAction("载入已保存的纸条设置", Load));
		panel.Children.Add(ActionButton("保存并应用纸条设置", _ =>
		{
			tray.SavePrintingPreferences(Draft());
			Report("纸条设置已应用；正在发送的任务使用发送前的设置。", InfoBarSeverity.Success);
			return Task.CompletedTask;
		}));
		var preview = new Image { MaxHeight = 360, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform };
		panel.Children.Add(ActionButton("示例纸条预览", async _ =>
		{
			var sample = new RelayJob { Id = new string('a', 40), Text = "Paperang Studio\n中文纸条示例", Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
			preview.Source = await RasterImage(new NoteRenderer().Render(sample, Draft()));
		}));
		panel.Children.Add(preview);
		relay.Children.Add(CollapsibleGroup("纸条样式与打印偏好", panel));
	}
}
