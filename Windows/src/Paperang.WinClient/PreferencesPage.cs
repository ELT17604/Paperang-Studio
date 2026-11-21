using Microsoft.UI.Xaml.Controls;
using Paperang.Windows;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private readonly StackPanel preferencesPage = new() { Spacing = 12 };
	private void BuildPreferences()
	{
		preferencesPage.Children.Add(Heading("设置与关于"));
		preferencesPage.Children.Add(Paragraph("Paperang Studio\n版本 " + Program.Version));
		var startup = new CheckBox { Content = "登录 Windows 后在后台启动 WinClient" };
		try { startup.IsChecked = trayHost?.Settings().StartAtLogin ?? StartupRegistration.Enabled; }
		catch (Exception error) { Report(error.Message, InfoBarSeverity.Error); }
		preferencesPage.Children.Add(startup);
		preferencesPage.Children.Add(ActionButton("应用启动偏好", _ =>
		{
			tray.SaveStartupPreference(startup.IsChecked == true);
			Report("Windows 启动偏好已保存。", InfoBarSeverity.Success);
			return Task.CompletedTask;
		}));
		preferencesPage.Children.Add(Paragraph("关闭窗口后可继续在后台接收纸条。通过托盘菜单可退出程序。"));
		preferencesPage.Children.Add(ActionButton("导出配置（不含凭证正文）…", async token =>
		{
			var picker = new FileSavePicker { SuggestedFileName = "Paperang-WinClient-config" };
			picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
			InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
			var file = await picker.PickSaveFileAsync();
			if (file == null) return;
			var settings = tray.Settings() with { DeviceKey = "" };
			var export = new { format = "paperang-winclient-config", schemaVersion = 1, version = Program.Version,
				exportedAt = DateTimeOffset.UtcNow, settings, workspace = ReadWorkspaceSettings(),
				note = "含私钥路径；不含私钥文件、密钥正文、会话、任务正文或待回报认领。" };
			await File.WriteAllTextAsync(file.Path, System.Text.Json.JsonSerializer.Serialize(export, Paperang.Core.RelayClient.Json), token);
		}));
		preferencesPage.Children.Add(new HyperlinkButton { Content = "GitHub 发布与更新", NavigateUri = new Uri("https://github.com/ELT17604/Paperang-Studio/releases") });
		preferencesPage.Children.Add(new HyperlinkButton { Content = "项目源码与使用说明", NavigateUri = new Uri("https://github.com/ELT17604/Paperang-Studio") });
	}
	internal Task StopTerminalSessionAsync() => terminal?.StopAsync() ?? Task.CompletedTask;
}
