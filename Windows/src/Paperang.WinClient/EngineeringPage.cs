using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Paperang.Core;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private readonly StackPanel engineeringPage = new() { Spacing = 12 };
	private readonly ComboBox queryCommand = new() { Header = "只读 GET 命令" };
	private readonly ListView readingsList = new() { Header = "设备采样", MaxHeight = 420 };
	private readonly List<DeviceReading> deviceReadings = [];
	private sealed record DeviceReading(int Command, string PayloadHex, DateTimeOffset SampledAt)
	{
		public override string ToString() => $"0x{Command:X2} · {PayloadHex} · {SampledAt.LocalDateTime:T}";
	}
	private void BuildEngineering()
	{
		engineeringPage.Children.Add(Heading("协议与设备字段"));
		engineeringPage.Children.Add(Paragraph("查询结果保留原始载荷和采样时间。查询超时会终止本次操作，不会发送写入命令。"));
		queryCommand.ItemsSource = P2Command.All.Where(command => P2Queries.Responses.ContainsKey(command.Code)).ToArray();
		queryCommand.SelectedIndex = 0;
		engineeringPage.Children.Add(queryCommand);
		engineeringPage.Children.Add(ActionButton("发送 GET 查询", async token =>
			await ReadDevice(((P2Command)queryCommand.SelectedItem).Code, token)));
		engineeringPage.Children.Add(ActionButton("读取全部已知 GET", async token =>
		{
			foreach (byte command in P2Queries.Responses.Keys)
			{
				token.ThrowIfCancellationRequested();
				await ReadDevice(command, token);
			}
		}));
		engineeringPage.Children.Add(readingsList);
		engineeringPage.Children.Add(ActionButton("导出会话 JSON…", async token =>
		{
			var picker = new FileSavePicker { SuggestedFileName = "Paperang-session" };
			picker.FileTypeChoices.Add("会话 JSON", new List<string> { ".json" });
			InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
			var file = await picker.PickSaveFileAsync();
			if (file == null) return;
			var payload = new { version = Program.Version, exportedAt = DateTimeOffset.UtcNow, readings = deviceReadings.ToArray(),
				jobs = trayHost?.Jobs ?? [], logs = trayHost?.Traces ?? [], settings = ReadWorkspaceSettings() };
			await File.WriteAllTextAsync(file.Path, JsonSerializer.Serialize(payload, RelayClient.Json), token);
		}));
		engineeringPage.Children.Add(SimpleAction("清空显示", () => { deviceReadings.Clear(); readingsList.ItemsSource = null; }));
		var catalogSearch = new TextBox { Header = "搜索命令、说明和验证等级" };
		var catalog = new ListView { ItemsSource = P2Command.All, MaxHeight = 280 };
		var catalogDetail = new TextBlock { TextWrapping = TextWrapping.Wrap };
		catalogSearch.TextChanged += (_, _) => catalog.ItemsSource = P2Command.All.Where(command =>
			$"{command} {command.Detail}".Contains(catalogSearch.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
		catalog.SelectionChanged += (_, _) => catalogDetail.Text = (catalog.SelectedItem as P2Command)?.Detail ?? "";
		var catalogPanel = new StackPanel { Spacing = 10 };
		catalogPanel.Children.Add(Paragraph("说明中的实测证据来自 Mac 项目的历史研究，不代表 Windows 硬件验证。只读查询使用上方 GET 目录。"));
		catalogPanel.Children.Add(catalogSearch); catalogPanel.Children.Add(catalog); catalogPanel.Children.Add(catalogDetail);
		engineeringPage.Children.Add(PropertyGroup("协议目录 · 37 条", catalogPanel));
		var transferPanel = new StackPanel { Spacing = 10 };
		transferPanel.Children.Add(Paragraph("默认值与 Android 一致。参数影响实际工作台/API 打印；USB 不使用 BLE 分段与写入间隔。较小载荷与较长间隔可能导致超时。"));
		foreach (var field in new FrameworkElement[] { transferFrame, transferChunk, transferWrite, transferPause, transferTimeout, transferFinish })
			transferPanel.Children.Add(field);
		transferPanel.Children.Add(ActionButton("导出完整参数预设…", token => PresetFile(true, token)));
		engineeringPage.Children.Add(PropertyGroup("传输参数", transferPanel));
		var raw = new StackPanel { Spacing = 10 };
		var commandHex = new TextBox { Header = "命令（十六进制）", Text = "04", MaxLength = 2 };
		var payloadHex = new TextBox { Header = "载荷（十六进制）", Text = "01", MaxLength = 32 };
		var unlock = new CheckBox { Content = "解锁工程参数写入（每次发送前仍确认）" };
		raw.Children.Add(TwoColumns(commandHex, payloadHex)); raw.Children.Add(unlock);
		raw.Children.Add(ActionButton("发送单帧（仅一次）…", async token =>
		{
			if (!byte.TryParse(commandHex.Text, System.Globalization.NumberStyles.HexNumber, null, out byte command))
				throw new InvalidDataException("请输入一个字节的十六进制命令。");
			if (P2Queries.Responses.ContainsKey(command)) { await ReadDevice(command, token); return; }
			if (unlock.IsChecked != true) throw new InvalidOperationException("请先解锁工程参数写入。");
			byte[] payload;
			try { payload = Convert.FromHexString(string.Concat(payloadHex.Text.Where(value => !char.IsWhiteSpace(value)))); }
			catch (FormatException) { throw new InvalidDataException("载荷必须是完整字节的十六进制。"); }
			P2Engineering.Validate(command, payload);
			if (!await Confirm(engineeringPage, $"发送工程指令 0x{command:X2}？",
				$"载荷：{Convert.ToHexString(payload)}。可能修改设备保存的参数；工厂模式取值及退出行为尚未验证。此操作不会自动恢复设置。")) return;
			await tray.SendEngineeringAsync(command, payload, ReadWorkspaceSettings(), token);
			Report("工程指令已取得确认，请核对设备实际状态。", InfoBarSeverity.Success);
		}));
		engineeringPage.Children.Add(PropertyGroup("工程单帧", raw));
		var logs = new ListView { Header = "协议日志 · 最近 500 帧", MaxHeight = 280 };
		engineeringPage.Children.Add(Paragraph("日志包含命令、载荷与收发时间；图像正文省略，其他载荷最多展示 256 字节。会话导出包含这些记录，请按需保存。"));
		engineeringPage.Children.Add(ActionButton("刷新通信日志", _ =>
		{
			logs.ItemsSource = (trayHost?.Traces ?? []).Reverse().ToArray();
			return Task.CompletedTask;
		}));
		engineeringPage.Children.Add(logs);
		engineeringPage.Children.Add(SimpleAction("清除通信日志", () => { trayHost?.ClearTraces(); logs.ItemsSource = null; }));
	}
	private async Task ReadDevice(byte command, CancellationToken token)
	{
		var payload = await tray.QueryAsync(command, token);
		deviceReadings.RemoveAll(reading => reading.Command == command);
		deviceReadings.Add(new(command, Convert.ToHexString(payload), DateTimeOffset.UtcNow));
		readingsList.ItemsSource = deviceReadings.OrderBy(reading => reading.Command).ToArray();
		Report($"GET 0x{command:X2} 已收到 {payload.Length} 字节。", InfoBarSeverity.Success);
	}
}
