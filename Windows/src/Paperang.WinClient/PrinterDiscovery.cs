using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Paperang.Windows;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private readonly ComboBox printerTransport = Choice("连接方式", "自动选择 USB，失败时使用蓝牙", "蓝牙");
	private readonly TextBox printerAddress = new() { Header = "蓝牙地址", MaxLength = 12, PlaceholderText = "扫描并选择设备，或输入 12 位地址" };
	private readonly ListView discoveredPrinters = new() { MinHeight = 100, MaxHeight = 300, SelectionMode = ListViewSelectionMode.Single };
	private readonly ObservableCollection<DiscoveredPrinter> visiblePrinters = [];
	private readonly Dictionary<ulong, DiscoveredPrinter> knownPrinters = [];
	private readonly CheckBox showAllPrinters = new() { Content = "显示其他蓝牙设备" };
	private readonly TextBlock scanStatus = Paragraph("打开打印机并靠近电脑，然后扫描附近设备。");
	private readonly ProgressRing scanActivity = new() { Width = 18, Height = 18, IsActive = false, Visibility = Visibility.Collapsed };
	private readonly ProgressRing connectionActivity = new() { Width = 20, Height = 20, IsActive = false, Visibility = Visibility.Collapsed };
	private readonly InfoBar printerConnectionStatus = new() { IsOpen = true, IsClosable = false, Title = "未连接打印机", Message = "选择设备后点击连接。", Severity = InfoBarSeverity.Informational };
	private readonly Button scanStart = new() { Content = "扫描设备" }, scanStop = new() { Content = "停止扫描", IsEnabled = false };
	private readonly Button connectPrinter = new() { Content = "连接打印机" }, cancelConnection = new() { Content = "取消连接", IsEnabled = false };
	private CancellationTokenSource? scannerLifetime, connectionLifetime;
	private Task? scanningTask;
	private bool connectionPending, printerPageClosed, previouslyConnected;
	private Func<bool, CancellationToken, IProgress<PrinterChoice>, Task<PrinterChoice[]>> scanProvider =
		(all, token, updates) => BlePrinter.ScanAsync(all, token, updates);

	internal sealed class DiscoveredPrinter(PrinterChoice value) : INotifyPropertyChanged
	{
		private PrinterChoice current = value;
		public ulong Address => current.Address;
		public string Name => current.Name;
		public string Detail => $"{Address:X12} · 信号 {current.Rssi} dBm";
		public event PropertyChangedEventHandler? PropertyChanged;
		public void Update(PrinterChoice next)
		{
			current = next;
			PropertyChanged?.Invoke(this, new(nameof(Name)));
			PropertyChanged?.Invoke(this, new(nameof(Detail)));
		}
	}
	private void BuildLivePrinterConnection()
	{
		var panel = new StackPanel { Spacing = 12 };
		panel.Children.Add(printerConnectionStatus);
		panel.Children.Add(connectionActivity);
		panel.Children.Add(printerTransport);
		panel.Children.Add(TwoColumns(connectPrinter, cancelConnection));
		connectPrinter.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
		connectPrinter.Click += async (_, _) => await ConnectPrinterSelection();
		cancelConnection.Click += (_, _) => connectionLifetime?.Cancel();
		var scanning = new StackPanel { Spacing = 12 };
		scanning.Children.Add(TwoColumns(scanStart, scanStop));
		scanning.Children.Add(scanActivity); scanning.Children.Add(scanStatus);
		scanning.Children.Add(showAllPrinters);
		discoveredPrinters.ItemsSource = visiblePrinters;
		discoveredPrinters.ItemTemplate = (DataTemplate)XamlReader.Load("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Spacing='4' Padding='4,8'><TextBlock Text='{Binding Name}' FontWeight='SemiBold'/><TextBlock Text='{Binding Detail}' FontSize='12' Opacity='0.7'/></StackPanel></DataTemplate>");
		scanning.Children.Add(discoveredPrinters);
		scanStart.Click += async (_, _) => await StartPrinterScan();
		scanStop.Click += (_, _) => scannerLifetime?.Cancel();
		showAllPrinters.Click += (_, _) => FilterDiscoveredPrinters();
		discoveredPrinters.SelectionChanged += (_, _) =>
		{
			if (discoveredPrinters.SelectedItem is DiscoveredPrinter selected)
			{
				printerAddress.Text = selected.Address.ToString("X12");
				printerTransport.SelectedIndex = 1;
				connectPrinter.Content = "连接 " + selected.Name;
				if (trayHost?.Connected != true && !connectionPending)
				{
					printerConnectionStatus.Title = "已选择 " + selected.Name;
					printerConnectionStatus.Message = "尚未连接。点击连接后会验证设备型号和纸仓状态。";
				}
			}
		};
		panel.Children.Add(PropertyGroup("附近的蓝牙设备", scanning));
		panel.Children.Add(CollapsibleGroup("手动输入蓝牙地址", printerAddress));
		printer.Children.Add(panel);
		Closed += (_, _) => { printerPageClosed = true; scannerLifetime?.Cancel(); connectionLifetime?.Cancel(); };
	}
	private async Task StartPrinterScan()
	{
		if (scannerLifetime != null || connectionPending || printerPageClosed) return;
		using var lifetime = new CancellationTokenSource();
		scannerLifetime = lifetime;
		knownPrinters.Clear(); visiblePrinters.Clear(); connectPrinter.Content = "连接打印机";
		scanStart.IsEnabled = false; scanStop.IsEnabled = true;
		scanActivity.IsActive = true; scanActivity.Visibility = Visibility.Visible;
		scanStatus.Text = "正在扫描 · 发现设备后即可选择和连接";
		var updates = new Progress<PrinterChoice>(value =>
		{
			if (!lifetime.IsCancellationRequested && !printerPageClosed && scannerLifetime == lifetime) ApplyDiscoveredPrinter(value);
		});
		try
		{
			var task = scanProvider(true, lifetime.Token, updates);
			scanningTask = task;
			foreach (var value in await task) if (!lifetime.IsCancellationRequested) ApplyDiscoveredPrinter(value);
			scanStatus.Text = visiblePrinters.Count == 0 ? "未发现设备。检查打印机电源、电脑蓝牙和 Windows 蓝牙权限后重试。" : $"扫描完成 · 发现 {visiblePrinters.Count} 台设备";
		}
		catch (OperationCanceledException) { scanStatus.Text = $"扫描已停止 · 保留 {visiblePrinters.Count} 台设备，可继续选择"; }
		catch (Exception error) { scanStatus.Text = "扫描未完成：" + error.Message; }
		finally
		{
			scannerLifetime = null; scanningTask = null;
			scanStart.Content = "重新扫描"; scanStart.IsEnabled = !connectionPending; scanStop.IsEnabled = false;
			scanActivity.IsActive = false; scanActivity.Visibility = Visibility.Collapsed;
		}
	}
	private void ApplyDiscoveredPrinter(PrinterChoice value)
	{
		if (knownPrinters.TryGetValue(value.Address, out var row)) row.Update(value);
		else { row = new(value); knownPrinters.Add(value.Address, row); }
		if ((showAllPrinters.IsChecked == true || BlePrinter.IsCandidate(row.Name)) && !visiblePrinters.Contains(row)) visiblePrinters.Add(row);
		if (scannerLifetime != null) scanStatus.Text = $"正在扫描 · 已发现 {visiblePrinters.Count} 台设备，可选择后直接连接";
	}
	private void FilterDiscoveredPrinters()
	{
		var selected = discoveredPrinters.SelectedItem;
		visiblePrinters.Clear();
		foreach (var row in knownPrinters.Values.Where(row => showAllPrinters.IsChecked == true || BlePrinter.IsCandidate(row.Name))) visiblePrinters.Add(row);
		if (selected is DiscoveredPrinter device && visiblePrinters.Contains(device)) discoveredPrinters.SelectedItem = device;
	}
	private async Task ConnectPrinterSelection()
	{
		if (connectionPending || printerPageClosed) return;
		connectionPending = true;
		using var lifetime = new CancellationTokenSource();
		connectionLifetime = lifetime;
		connectPrinter.IsEnabled = false; cancelConnection.IsEnabled = true; scanStart.IsEnabled = false;
		connectionActivity.IsActive = true; connectionActivity.Visibility = Visibility.Visible;
		try
		{
			if (operation != null) throw new InvalidOperationException("请等待当前操作完成后再连接打印机。");
			scannerLifetime?.Cancel();
			if (scanningTask != null) await scanningTask.ContinueWith(_ => { });
			string address = printerAddress.Text.Trim();
			bool validAddress = address.Length == 12 && address != "000000000000" && ulong.TryParse(address, System.Globalization.NumberStyles.HexNumber, null, out _);
			if ((printerTransport.SelectedIndex == 1 || address.Length > 0) && !validAddress)
				throw new InvalidDataException("请先选择蓝牙打印机，或输入 12 位十六进制地址。");
			var saved = tray.Settings();
			lifetime.Token.ThrowIfCancellationRequested();
			var selected = discoveredPrinters.SelectedItem as DiscoveredPrinter;
			string name = selected?.Address.ToString("X12") == address ? selected.Name :
				saved.PrinterAddress.Equals(address, StringComparison.OrdinalIgnoreCase) ? saved.PrinterName : "P2";
			printerConnectionStatus.Severity = InfoBarSeverity.Informational;
			printerConnectionStatus.Title = "正在连接打印机…";
			printerConnectionStatus.Message = printerTransport.SelectedIndex == 1 ? $"蓝牙 · {name} · {address}，正在验证 P2 和读取设备状态。" : "正在检测 USB；不可用时将尝试已保存的蓝牙设备。";
			tray.SaveSettings(saved with { PrinterAddress = address, PrinterName = name, PrinterTransport = printerTransport.SelectedIndex == 1 ? "bluetooth" : "auto" });
			await tray.UseLocalPrinterAsync(null, new(), lifetime.Token);
			printerConnectionStatus.Title = "打印机已连接";
			printerConnectionStatus.Message = tray.DeviceStatus;
			printerConnectionStatus.Severity = InfoBarSeverity.Success;
		}
		catch (OperationCanceledException) { printerConnectionStatus.Title = "连接已取消"; printerConnectionStatus.Message = "可以重新选择设备并连接。"; printerConnectionStatus.Severity = InfoBarSeverity.Informational; }
		catch (Exception error) { printerConnectionStatus.Title = "未能连接打印机"; printerConnectionStatus.Message = error.Message; printerConnectionStatus.Severity = InfoBarSeverity.Error; }
		finally
		{
			connectionPending = false; connectionLifetime = null; cancelConnection.IsEnabled = false;
			connectPrinter.IsEnabled = true; scanStart.IsEnabled = scannerLifetime == null;
			connectionActivity.IsActive = false; connectionActivity.Visibility = Visibility.Collapsed;
		}
	}
	private void RefreshPrinterConnection()
	{
		if (connectionPending) return;
		bool connected = trayHost?.Connected == true;
		if (connected)
		{
			printerConnectionStatus.Title = trayHost!.Working ? "正在与打印机通信" : "打印机已连接";
			printerConnectionStatus.Message = trayHost.DeviceStatus;
			printerConnectionStatus.Severity = InfoBarSeverity.Success;
		}
		else if (previouslyConnected)
		{
			printerConnectionStatus.Title = "打印机已断开"; printerConnectionStatus.Message = "选择设备后可重新连接。";
			printerConnectionStatus.Severity = InfoBarSeverity.Warning;
		}
		previouslyConnected = connected;
	}
}
