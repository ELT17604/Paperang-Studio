using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Paperang.Core;
using Paperang.Windows;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private readonly StackPanel terminalPage = new() { Spacing = 12 };
	private readonly TextBox terminalDirectory = new() { Header = "工作目录", Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
	private readonly ComboBox terminalShell = Choice("Shell", "cmd.exe", "Windows PowerShell");
	private readonly NumberBox terminalColumns = Number("终端列数", 20, 200, 80);
	private readonly NumberBox terminalRows = Number("终端行数", 5, 100, 24);
	private readonly CheckBox terminalProfile = new() { Content = "加载 PowerShell 用户配置" };
	private readonly TextBox terminalScreen = new() { Header = "终端输出", IsReadOnly = true, AcceptsReturn = true,
		TextWrapping = TextWrapping.Wrap, Height = 300, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
	private readonly TextBox terminalInput = new() { Header = "输入命令或交互式回答", MaxLength = 8192 };
	private readonly PasswordBox terminalSecret = new() { Header = "隐私输入", MaxLength = 8192, Visibility = Visibility.Collapsed };
	private readonly CheckBox terminalPrivacy = new() { Content = "隐私输入：暂停显示与记录" };
	private readonly CheckBox terminalAutomatic = new() { Content = "自动打印终端输出" };
	private readonly NumberBox terminalIdle = Number("空闲多少秒后自动打印", 1, 60, 3);
	private readonly NumberBox terminalFont = Number("纸带字号", 8, 32, 16);
	private readonly NumberBox terminalFeed = Number("每批额外送纸（行）", 0, 300, 0);
	private readonly TextBlock terminalStatus = Paragraph("终端未运行；默认不自动耗纸。");
	private TerminalSession? terminal;
	private readonly TerminalBuffer terminalBuffer = new();
	private DateTime lastTerminalOutput = DateTime.UtcNow;
	private int terminalPrivate, terminalEpoch;

	private void BuildTerminal()
	{
		terminalPage.Children.Add(Heading("纸带终端"));
		var manual = new TextBox { Header = "手动输入纸带文本", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 150, MaxLength = 12000 };
		terminalPage.Children.Add(manual);
		terminalPage.Children.Add(SimpleAction("将文本加入待打印缓冲", () =>
		{
			if (string.IsNullOrWhiteSpace(manual.Text)) throw new InvalidDataException("请输入纸带文本。");
			if (terminalBuffer.PendingCount + manual.Text.Length + 1 > 12000) throw new InvalidOperationException("待打印缓冲不足，请先打印或丢弃已有内容。");
			terminalBuffer.Append(manual.Text.TrimEnd('\r', '\n') + "\n");
			manual.Text = ""; terminalScreen.Text = terminalBuffer.Transcript;
			lastTerminalOutput = DateTime.UtcNow;
			terminalStatus.Text = $"待打印 {terminalBuffer.PendingCount} 字符";
		}));
		terminalPage.Children.Add(Paragraph("真实 Windows ConPTY 会话。面向命令行文字输出，不提供全屏终端应用的完整屏幕仿真。启用自动打印前请先确认输出内容。"));
		foreach (var field in new FrameworkElement[] { terminalDirectory, terminalShell, terminalProfile }) terminalPage.Children.Add(field);
		terminalPage.Children.Add(TwoColumns(terminalColumns, terminalRows));
		terminalPage.Children.Add(ActionButton("选择工作目录…", async _ =>
		{
			var picker = new FolderPicker();
			picker.FileTypeFilter.Add("*");
			InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
			var folder = await picker.PickSingleFolderAsync();
			if (folder != null) terminalDirectory.Text = folder.Path;
		}));
		terminalPage.Children.Add(ActionButton("启动终端", StartTerminal));
		terminalPage.Children.Add(terminalStatus);
		terminalPage.Children.Add(terminalScreen);
		terminalPage.Children.Add(terminalInput);
		terminalPage.Children.Add(terminalSecret);
		terminalInput.KeyDown += TerminalEnter;
		terminalSecret.KeyDown += TerminalEnter;
		terminalPage.Children.Add(SimpleAction("发送回车", SendTerminalLine));
		var keys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		foreach (var (label, value) in new[] { ("Ctrl-C", "\x03"), ("Ctrl-D", "\x04"), ("Ctrl-Z", "\x1a"), ("Tab", "\t"), ("↑", "\x1b[A"), ("↓", "\x1b[B") })
			keys.Children.Add(SimpleAction(label, () => ActiveTerminal().Send(value)));
		terminalPage.Children.Add(keys);
		terminalPage.Children.Add(ActionButton("停止 Shell", async _ =>
		{
			terminalAutomatic.IsChecked = false;
			if (terminal != null) await terminal.StopAsync();
			terminalStatus.Text = "Shell 已停止，未打印的缓冲仍保留。";
		}));
		terminalPrivacy.Click += (_, _) =>
		{
			bool value = terminalPrivacy.IsChecked == true;
			Volatile.Write(ref terminalPrivate, value ? 1 : 0);
			Interlocked.Increment(ref terminalEpoch);
			terminalBuffer.ClearPartialLine();
			terminalInput.Text = ""; terminalSecret.Password = "";
			terminalInput.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
			terminalSecret.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
		};
		terminalPage.Children.Add(terminalPrivacy);
		terminalPage.Children.Add(terminalAutomatic);
		terminalPage.Children.Add(TwoColumns(terminalIdle, terminalFont));
		terminalPage.Children.Add(terminalFeed);
		terminalPage.Children.Add(ActionButton("立即打印缓冲", PrintTerminal));
		terminalPage.Children.Add(ActionButton("丢弃待打印缓冲…", async _ =>
		{
			if (await Confirm(terminalPage, "丢弃未提交的纸带缓冲？", $"{terminalBuffer.PendingCount} 字符"))
				terminalBuffer.Discard();
		}));
		terminalPage.Children.Add(ActionButton("导出终端文本…", async token =>
		{
			var picker = new FileSavePicker { SuggestedFileName = "Paperang-terminal" };
			picker.FileTypeChoices.Add("文字", new List<string> { ".txt" });
			InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
			var file = await picker.PickSaveFileAsync();
			if (file != null) await File.WriteAllTextAsync(file.Path, terminalBuffer.Transcript, token);
		}));
		var timer = DispatcherQueue.CreateTimer();
		timer.Interval = TimeSpan.FromSeconds(1);
		timer.Tick += async (_, _) =>
		{
			if (terminalAutomatic.IsChecked != true || operation != null || terminalBuffer.PendingCount == 0) return;
			if (trayHost?.AllowPaper != true || terminalBuffer.Overflowed)
			{
				terminalAutomatic.IsChecked = false;
				terminalStatus.Text = "自动打印已暂停：耗纸未开启或缓冲达到上限。";
				return;
			}
			if (Volatile.Read(ref terminalPrivate) != 0 || tray.Working ||
			    DateTime.UtcNow - lastTerminalOutput < TimeSpan.FromSeconds(Value(terminalIdle))) return;
			using var cancellation = new CancellationTokenSource();
			operation = cancellation;
			try { await PrintTerminal(cancellation.Token); }
			catch (Exception error) { terminalAutomatic.IsChecked = false; Report(error.Message, InfoBarSeverity.Warning); }
			finally { operation = null; }
		};
		timer.Start();
		Closed += async (_, _) =>
		{
			timer.Stop();
			terminalAutomatic.IsChecked = false;
			if (terminal != null) await terminal.StopAsync();
		};
	}
	private TerminalSession ActiveTerminal() => terminal?.Running == true ? terminal : throw new InvalidOperationException("终端未运行。");
	private void TerminalEnter(object sender, KeyRoutedEventArgs args)
	{
		if (args.Key != global::Windows.System.VirtualKey.Enter) return;
		args.Handled = true;
		try { SendTerminalLine(); }
		catch (Exception error) { Report(error.Message, InfoBarSeverity.Error); }
	}
	private void SendTerminalLine()
	{
		string value = terminalPrivacy.IsChecked == true ? terminalSecret.Password : terminalInput.Text;
		ActiveTerminal().Send(value + "\r");
		terminalInput.Text = ""; terminalSecret.Password = "";
	}
	private async Task StartTerminal(CancellationToken token)
	{
		if (terminal?.Running == true) throw new InvalidOperationException("终端正在运行。");
		if (terminalBuffer.PendingCount > 0) throw new InvalidOperationException("请先打印或丢弃上一会话的缓冲。");
		if (terminal != null) await terminal.StopAsync();
		terminal = new();
		terminal.Output += value =>
		{
			bool privateInput = Volatile.Read(ref terminalPrivate) != 0;
			int epoch = Volatile.Read(ref terminalEpoch);
			DispatcherQueue.TryEnqueue(() =>
			{
				if (epoch != terminalEpoch) return;
				terminalBuffer.Append(value, !privateInput);
				terminalScreen.Text = terminalBuffer.Transcript;
				lastTerminalOutput = DateTime.UtcNow;
				terminalStatus.Text = terminalBuffer.Overflowed ? "待打印缓冲已满。额外输出仅保留在终端历史，请导出后检查。" :
					$"ConPTY · 待打印 {terminalBuffer.PendingCount} 字符";
			});
		};
		terminal.Exited += () => DispatcherQueue.TryEnqueue(() => terminalStatus.Text = "Shell 已退出，缓冲仍可打印。");
		string directory = terminalDirectory.Text;
		int columns = Value(terminalColumns), rows = Value(terminalRows);
		bool powershell = terminalShell.SelectedIndex == 1, profile = terminalProfile.IsChecked == true;
		await Task.Run(() => terminal.Start(directory, columns, rows, powershell, profile), token);
		terminalStatus.Text = "ConPTY 会话已启动，默认不自动耗纸。";
	}
	private async Task PrintTerminal(CancellationToken token)
	{
		if (terminalPrivacy.IsChecked == true) throw new InvalidOperationException("隐私输入期间不打印。");
		int maximum = 1000;
		Raster output;
		string value;
		int size = Value(terminalFont), selectedDensity = Value(density), trailingFeed = Value(terminalFeed);
		while (true)
		{
			value = terminalBuffer.Peek(maximum);
			if (value.Length == 0) throw new InvalidOperationException("没有已完成行的纸带缓冲。");
			try { output = await Task.Run(() => NoteRenderer.RenderText(value, "Consolas", size, false, "居左", 0, 0, 2), token); break; }
			catch (InvalidDataException) when (maximum > 1) { maximum /= 2; }
		}
		await tray.UseLocalPrinterAsync(output, new(selectedDensity, After: trailingFeed), token);
		terminalBuffer.Commit(value);
		terminalStatus.Text = $"本批已发送，请核对纸面；剩余 {terminalBuffer.PendingCount} 字符。";
	}
}
