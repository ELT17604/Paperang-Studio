using Paperang.Core;
using System.Reflection;

namespace Paperang.WinLite;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\ELT.PaperangStudio.WinLite", out bool first);
        using var showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ELT.PaperangStudio.WinLite.Settings");
        if (!first) { if (!args.Contains("--background")) showSettings.Set(); return; }
        using var context = new TrayContext(showSettings, args.Contains("--settings"));
        Application.Run(context);
        mutex.ReleaseMutex();
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new();
    private readonly ToolStripMenuItem server = new(), bluetooth = new(), print = new(), permission = new(), connection = new();
    private readonly Control dispatcher = new();
    private readonly LocalStore store = new();
    private readonly BlePrinter printer = new();
    private readonly RelayEngine engine;
    private AppSettings settings = new();
    private SettingsForm? form;
    private readonly RegisteredWaitHandle signal;
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 1000 };
    private bool exiting;
    private string storageError = "";
    private int updateQueued;
    public TrayContext(EventWaitHandle showSettings, bool openSettings)
    {
        dispatcher.CreateControl();
        engine = new RelayEngine(printer, new NoteRenderer(), store);
        engine.Changed += Update;
        try { settings = store.LoadSettings(); }
        catch { storageError = "本地凭证读取失败，请打开设置重新保存；未回报任务记录会保留"; }
        try { StartupRegistration.RepairIfEnabled(); }
        catch { storageError = "登录启动路径更新失败，请在设置中重新保存自启选项。"; }
        using var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Paperang.AppIcon.ico")!;
        tray = new NotifyIcon { Icon = new Icon(iconStream), Text = "Paperang Studio " + AppVersion.Current, ContextMenuStrip = menu, Visible = true };
        var title = new ToolStripMenuItem("Paperang Studio · V" + AppVersion.Current) { Enabled = false };
        foreach (var item in new[] { server, bluetooth, print }) item.Enabled = false;
        permission.Click += (_, _) => engine.SetAllowed(!engine.Allowed);
        connection.Click += async (_, _) => {
            if (engine.Running) await Stop(); else await Connect();
        };
        menu.Items.AddRange([title, new ToolStripSeparator(), server, bluetooth, print, new ToolStripSeparator(), permission, connection,
            new ToolStripMenuItem("设置…", null, (_, _) => ShowSettings()),
            new ToolStripMenuItem("关于…", null, (_, _) => MessageBox.Show("Paperang Studio V" + AppVersion.Current + "\n© 2026 ELT Group\n\nWindows 原生托盘客户端 · BLE P2 · 在线传纸条\nCC BY-NC-SA 4.0 · 独立非官方项目\nvox@calx.cc", "关于 Paperang Studio", MessageBoxButtons.OK, MessageBoxIcon.Information)),
            new ToolStripSeparator(), new ToolStripMenuItem("退出", null, async (_, _) => await Quit())]);
        tray.DoubleClick += (_, _) => ShowSettings();
        tray.BalloonTipClicked += (_, _) => ShowSettings();
        signal = ThreadPool.RegisterWaitForSingleObject(showSettings, (_, _) => Dispatch(ShowSettings), null, Timeout.Infinite, false);
        refresh.Tick += (_, _) => Update(); refresh.Start();
        Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Update();
        dispatcher.BeginInvoke(async () => {
            if (openSettings) ShowSettings();
            if (storageError.Length != 0) Notify(storageError);
            else if (settings.AutoConnect && settings.Server.Length > 0) await Connect();
            else Notify("已在托盘运行。双击打印机图标打开设置，连接服务器与 P2。");
        });
    }
    private async Task Connect()
    {
        try { await engine.StartAsync(settings); }
        catch (Exception e) { Notify(e.Message); }
        Update();
    }
    private async Task Stop() { await engine.StopAsync(); Update(); }
    private void Dispatch(Action action)
    {
        if (exiting || dispatcher.IsDisposed) return;
        if (dispatcher.InvokeRequired) { try { dispatcher.BeginInvoke(action); } catch (InvalidOperationException) { } } else action();
    }
    private void Update()
    {
        if (Interlocked.Exchange(ref updateQueued, 1) != 0) return;
        Dispatch(() => {
        Interlocked.Exchange(ref updateQueued, 0);
        server.Text = engine.Online ? $"中转：在线 · {engine.Queued} 项待处理" : engine.Message;
        bluetooth.Text = printer.Status;
        print.Text = engine.Busy ? $"打印：{engine.Progress:P0} · {engine.PrintMessage}" : engine.PrintMessage;
        permission.Text = "开启自动打印";
        permission.Checked = engine.Allowed;
        connection.Text = engine.Running ? "断开连接" : "重新连接";
        tray.Text = "Paperang Studio · " + (engine.Busy ? $"发送 {engine.Progress:P0}" : engine.Online ? engine.Allowed ? "在线 · 自动打印" : "在线 · 已暂停" : "中转未连接");
        form?.UpdateStatus();
        });
    }
    private void ShowSettings()
    {
        if (form is { IsDisposed: false }) { form.WindowState = FormWindowState.Normal; form.Show(); form.Activate(); return; }
        form = new SettingsForm(settings, engine, async (value, startup) => {
            value.Validate();
            if (engine.Busy) throw new InvalidOperationException("请等待当前打印完成后保存");
            // Validate journal affinity before replacing credentials on disk.
            var pending = store.Load();
            if (pending != null && (pending.Server != value.Validate().AbsoluteUri || pending.KeyFingerprint != RelayEngine.Fingerprint(value.DeviceKey)))
                throw new InvalidOperationException("有未回报结果，请先使用原服务器和密钥重新连接");
            StartupRegistration.Set(startup);
            store.SaveSettings(value); settings = value; storageError = "";
            await engine.StartAsync(settings);
        }, (templates, id) => {
            var next = settings with { Templates = templates, TemplateId = id, Style = "自定义" };
            foreach (var template in templates) template.Validate();
            store.SaveSettings(next); settings = next; engine.ApplyTemplates(templates, id);
        }, value => {
            var next = settings.WithPrintingFrom(value);
            foreach (var template in next.Templates) template.Validate();
            store.SaveSettings(next); settings = next; engine.ApplyPrintingPreferences(next);
        });
        form.Show();
    }
    private void Notify(string text)
    {
        tray.BalloonTipTitle = "Paperang Studio"; tray.BalloonTipText = text; tray.ShowBalloonTip(5000);
    }
    private void OnSessionEnding(object? sender, Microsoft.Win32.SessionEndingEventArgs e)
    {
        // The journal was flushed before the first printer byte. Recovery reports unknown.
        engine.SetAllowed(false);
    }
    private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        // Resume invalidates the old GATT session; active sends settle as unknown.
        if (e.Mode == Microsoft.Win32.PowerModes.Resume) engine.InvalidatePrinter();
    }
    private async Task Quit()
    {
        if (exiting) return;
        exiting = true; tray.Visible = false; refresh.Stop();
        await engine.StopAsync();
        form?.Close(); ExitThread();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding;
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            signal.Unregister(null); refresh.Dispose(); tray.Icon?.Dispose(); tray.Dispose(); menu.Dispose(); printer.Dispose(); dispatcher.Dispose();
        }
        base.Dispose(disposing);
    }
}
