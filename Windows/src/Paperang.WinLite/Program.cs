using Paperang.Core;
using System.Reflection;

namespace Paperang.WinLite;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--version")) { Console.WriteLine(AppVersion.Current); return; }
        if (args.Length == 2 && args[0] == "--configure-owner")
        {
            try { ConfigureOwner(args[1]).GetAwaiter().GetResult(); }
            catch (Exception e) { Console.Error.WriteLine(e is ArgumentException or InvalidDataException or InvalidOperationException ? e.Message : "所有者连接验证失败，旧配置保持不变"); Environment.ExitCode = 1; }
            return;
        }
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\ELT.PaperangStudio.WinLite", out bool first);
        using var showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ELT.PaperangStudio.WinLite.Settings");
        if (!first) { if (!args.Contains("--background")) showSettings.Set(); return; }
        using var context = new TrayContext(showSettings, args.Contains("--settings"));
        Application.Run(context);
        mutex.ReleaseMutex();
    }
    private static async Task ConfigureOwner(string path)
    {
        var store = new LocalStore();
        if (store.Load() != null) throw new InvalidOperationException("先完成旧待回报任务，再切换所有者身份");
        var next = store.LoadSettings() with { AuthenticationMode = "owner", OwnerKeyFile = Path.GetFullPath(path), DeviceKey = "" };
        using var client = new RelayClient(next);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var me = await client.Get("v2/auth/me", timeout.Token);
        if (me.GetProperty("role").GetString() != "C") throw new InvalidDataException("私钥未获得 C 管理权限");
        await client.Get("v2/device/jobs", timeout.Token);
        store.SaveSettings(next); store.SaveDraft(next);
        Console.WriteLine("所有者配对及执行权限已验证；旧密钥值已清除，模板和打印设置已保留");
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
        form = new SettingsForm(store.LoadDraft() ?? settings, engine, async (value, startup) => {
            value.Validate();
            if (engine.Busy) throw new InvalidOperationException("请等待当前打印完成后保存");
            // Validate journal affinity before replacing credentials on disk.
            var pending = store.Load();
            if (pending != null && (pending.Server != value.Validate().AbsoluteUri || pending.KeyFingerprint != RelayEngine.CredentialFingerprint(value)))
                throw new InvalidOperationException("有未回报结果，请先使用原服务器和密钥重新连接");
            StartupRegistration.Set(startup);
            store.SaveSettings(value); store.SaveDraft(value); settings = value; storageError = "";
            await engine.StartAsync(settings);
        }, (templates, id) => {
            var next = settings with { Templates = templates, TemplateId = id, Style = "自定义" };
            foreach (var template in templates) template.Validate();
            store.SaveSettings(next); settings = next; engine.ApplyTemplates(templates, id);
        }, (value, startup) => {
            foreach (var template in value.Templates) template.Validate();
            value = value with { StartAtLogin = startup };
            store.SaveDraft(value);
            if (StartupRegistration.Enabled != startup) StartupRegistration.Set(startup);
            var next = settings.WithPrintingFrom(value) with { AutoConnect = value.AutoConnect, KeepReconnecting = value.KeepReconnecting, StartAtLogin = startup };
            string result = "全部设置已自动保存；连接变更可点击“保存并连接”立即应用。";
            try {
                var uri = value.Validate(); var pending = store.Load();
                if (pending == null || pending.Server == uri.AbsoluteUri && pending.KeyFingerprint == RelayEngine.CredentialFingerprint(value)) next = value;
                else result = "全部草稿已保存；先完成原连接的待回报任务，再切换服务器或私钥。";
            } catch (ArgumentException) { result = "全部草稿已保存；连接尚未填完整，后台继续使用原连接。"; }
              catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException or System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException) { result = "全部草稿已保存；请检查私钥文件，后台继续使用原连接。"; }
            store.SaveSettings(next); settings = next; engine.ApplySavedPreferences(next);
            return result;
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
