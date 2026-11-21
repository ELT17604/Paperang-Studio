using Microsoft.UI.Xaml.Controls;
using Paperang.Core;
using Paperang.Windows;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private void BuildConnectionSettings()
	{
		var server = new TextBox { Header = "中转服务器根地址", PlaceholderText = "https://relay.example.com" };
		var owner = new TextBox { Header = "所有者私钥文件路径" };
		var authentication = Choice("执行凭证", "所有者私钥", "设备密钥（兼容配置）");
		var deviceKey = new PasswordBox { Header = "设备执行密钥", MaxLength = 512 };
		var allowHttp = new CheckBox { Content = "允许公网 HTTP 连接" };
		var autoConnect = new CheckBox { Content = "启动后自动连接中转" };
		var reconnect = new CheckBox { Content = "本轮失败后继续重试" };
		AppSettings? saved = null;
		try
		{
			saved = trayHost?.Settings() ?? new();
			printerAddress.Text = saved.PrinterAddress;
			printerTransport.SelectedIndex = saved.PrinterTransport == "bluetooth" ? 1 : 0;
			server.Text = saved.Server;
			owner.Text = saved.OwnerKeyFile;
			authentication.SelectedIndex = saved.AuthenticationMode == "owner" ? 0 : 1;
			deviceKey.Password = saved.DeviceKey;
			allowHttp.IsChecked = saved.AllowHttp;
			autoConnect.IsChecked = saved.AutoConnect;
			reconnect.IsChecked = saved.KeepReconnecting;
		}
		catch (Exception error) { Report(error.Message, InfoBarSeverity.Error); }
		BuildLivePrinterConnection();
		relayConnection.Children.Add(server);
		relayConnection.Children.Add(authentication);
		relayConnection.Children.Add(owner);
		relayConnection.Children.Add(deviceKey);
		void UpdateAuthentication()
		{
			owner.Visibility = authentication.SelectedIndex == 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
			deviceKey.Visibility = authentication.SelectedIndex == 1 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
		}
		authentication.SelectionChanged += (_, _) => UpdateAuthentication();
		UpdateAuthentication();
		foreach (var field in new Microsoft.UI.Xaml.FrameworkElement[] { allowHttp, autoConnect, reconnect })
			relayConnection.Children.Add(field);
		relayConnection.Children.Add(ActionButton("选择已配对的私钥文件…", async _ =>
		{
			var picker = new FileOpenPicker();
			picker.FileTypeFilter.Add("*");
			InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
			var file = await picker.PickSingleFileAsync();
			if (file != null) owner.Text = file.Path;
		}));
		void SaveConnection()
		{
			if (saved == null)
				throw new InvalidOperationException("原设置读取失败，禁止覆盖。");
			var current = tray.Settings();
			var next = current with {
				AuthenticationMode = authentication.SelectedIndex == 0 ? "owner" : "legacy", DeviceKey = deviceKey.Password.Trim(),
				Server = server.Text.Trim(), OwnerKeyFile = owner.Text.Trim(), AllowHttp = allowHttp.IsChecked == true,
				AutoConnect = autoConnect.IsChecked == true, KeepReconnecting = reconnect.IsChecked == true };
			if (next.Server.Length != 0)
				next.Validate();
			tray.SaveSettings(next);
			saved = next;
			Report("连接设置已加密保存。", InfoBarSeverity.Success);
		}
		relayConnection.Children.Add(SimpleAction("保存服务器配置", SaveConnection));
	}
}
