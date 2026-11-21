using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Paperang.Windows;

namespace Paperang.WinClient;

internal static class Program
{
	internal const string Version = "0.3.3-winclient-dev5";

	[STAThread]
	private static void Main(string[] args)
	{
		Console.OutputEncoding = System.Text.Encoding.UTF8;
#if !PAPERANG_DESKTOP_CHECKS
		if (args.Contains("--self-test"))
		{
			Console.Error.WriteLine("Desktop checks require the separate verification build.");
			Environment.ExitCode = 2;
			return;
		}
#endif
		if (args.Contains("--version"))
		{
			Console.WriteLine(Version);
			return;
		}
		if (args.Contains("--shutdown"))
		{
			Signal("Shutdown");
			return;
		}
		using var instance = new Mutex(true, @"Local\ELT.PaperangStudio.WinLite", out bool first);
		if (!first && !args.Contains("--self-test") && !args.Contains("--workspace-check") && !args.Contains("--smoke-test"))
		{
			if (!args.Contains("--background") && !Signal("Show"))
				System.Windows.Forms.MessageBox.Show("请先退出正在运行的 WinLite，然后启动 WinClient。");
			return;
		}
		WinRT.ComWrappersSupport.InitializeComWrappers();
		Application.Start(initialization =>
		{
			var dispatcher = DispatcherQueue.GetForCurrentThread();
			SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(dispatcher));
			_ = new StudioApp(dispatcher, args);
		});
	}
	private static bool Signal(string action)
	{
		try
		{
			using var request = EventWaitHandle.OpenExisting(@"Local\ELT.PaperangStudio.WinClient." + action);
			return request.Set();
		}
		catch (WaitHandleCannotBeOpenedException) { return false; }
	}
}

public sealed partial class StudioApp : Application
{
	private readonly DispatcherQueue dispatcher;
	private readonly string[] args;
	private WorkspaceWindow? workspace;
	private ClientSession? tray;
	private System.Windows.Forms.NotifyIcon? notification;
	private readonly List<(EventWaitHandle Event, RegisteredWaitHandle Wait)> signals = [];
	private bool quitting;

	public StudioApp(DispatcherQueue dispatcher, string[] args)
	{
		this.dispatcher = dispatcher;
		this.args = args;
		RequestedTheme = ApplicationTheme.Light;
		UnhandledException += (_, failure) => Console.Error.WriteLine(failure.Exception);
		InitializeComponent();
	}

	protected override void OnLaunched(LaunchActivatedEventArgs launch)
	{
		if (args.Contains("--workspace-check") || args.Contains("--self-test") || args.Contains("--smoke-test"))
		{
			try
			{
				workspace = new WorkspaceWindow(null);
				workspace.Activate();
				if (args.Contains("--self-test") || args.Contains("--smoke-test"))
				{
					var timer = dispatcher.CreateTimer();
					timer.Interval = TimeSpan.FromSeconds(args.Contains("--self-test") ? 5 : 1);
					timer.IsRepeating = false;
					timer.Tick += async (_, _) =>
					{
						try
						{
							await Task.Yield();
							if (workspace.Content is not FrameworkElement { IsLoaded: true }) throw new InvalidOperationException("Workspace did not load");
#if PAPERANG_DESKTOP_CHECKS
							if (args.Contains("--self-test")) await workspace.CheckWorkspaceAsync();
#endif
							Console.WriteLine("PASS desktop startup and WinUI resources");
						}
						catch (Exception error)
						{
							Console.Error.WriteLine(error);
							Environment.ExitCode = 1;
						}
						finally { workspace.Close(); Exit(); }
					};
					timer.Start();
				}
			}
			catch (Exception error)
			{
				Console.Error.WriteLine(error);
				Environment.ExitCode = 1;
				Exit();
			}
			return;
		}
		tray = new ClientSession();
		Listen("Shutdown", () => _ = QuitAsync());
		Listen("Show", ShowWorkspace);
		using var icon = typeof(StudioApp).Assembly.GetManifestResourceStream("Paperang.AppIcon.ico")!;
		var menu = new System.Windows.Forms.ContextMenuStrip();
		menu.Items.Add("打开工作台", null, (_, _) => dispatcher.TryEnqueue(ShowWorkspace));
		menu.Items.Add("退出", null, async (_, _) => await QuitAsync());
		notification = new System.Windows.Forms.NotifyIcon { Icon = new System.Drawing.Icon(icon), Text = "Paperang Studio · WinClient",
			ContextMenuStrip = menu, Visible = true };
		notification.DoubleClick += (_, _) => dispatcher.TryEnqueue(ShowWorkspace);
		if (!args.Contains("--background"))
			ShowWorkspace();
		_ = RestoreReceiver();
	}
	private void Listen(string action, Action callback)
	{
		var signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ELT.PaperangStudio.WinClient." + action);
		var wait = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) => dispatcher.TryEnqueue(() => callback()), null, Timeout.Infinite, false);
		signals.Add((signal, wait));
	}
	private async Task QuitAsync()
	{
		if (quitting) return;
		quitting = true;
		try
		{
			tray?.SetPaperAllowed(false);
			if (workspace != null) await workspace.StopTerminalSessionAsync();
			if (tray != null) await tray.StopAsync();
		}
		catch (Exception error) { Console.Error.WriteLine(error); }
		finally
		{
			foreach (var signal in signals) { signal.Wait.Unregister(null); signal.Event.Dispose(); }
			notification?.Dispose(); tray?.Dispose(); workspace?.Close(); Exit();
		}
	}
	private async Task RestoreReceiver()
	{
		try
		{
			var saved = tray!.Settings();
			if (saved.AutoConnect && !string.IsNullOrWhiteSpace(saved.Server))
				await tray.SetRelayAsync(true);
		}
		catch (Exception error)
		{
			notification?.ShowBalloonTip(5000, "中转未连接", error.Message, System.Windows.Forms.ToolTipIcon.Warning);
		}
	}

	private void ShowWorkspace()
	{
		if (tray == null)
			return;
		if (workspace == null)
		{
			workspace = new WorkspaceWindow(tray);
			workspace.Closed += (_, _) => workspace = null;
		}
		workspace.Activate();
	}
}
