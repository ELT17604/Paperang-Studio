using Paperang.Core;
using Paperang.WinLite;

static class UsbCases
{
    public static async Task Cycle()
    {
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(100));
        var original = new LocalStore().LoadSettings();
        using var printer = new PreferredPrinter(new UsbPrinter(), new BlePrinter());
        await printer.ConnectAsync(original with { PrinterTransport = "auto" }, ct.Token);
        if (printer.ActiveTransport != "usb") throw new Exception("USB priority not selected");
        Console.WriteLine("PASS actual USB priority and BLE suppression");
        printer.Disconnect();
        // Refresh the existing advertisement cache before the unchanged BLE backend.
        var rows = await BlePrinter.ScanAsync(false, ct.Token);
        if (!rows.Any(x => x.Address.ToString("X12").Equals(original.PrinterAddress, StringComparison.OrdinalIgnoreCase)))
            throw new Exception("Remembered P2 not advertising; no alternate device selected");
        await printer.ConnectAsync(original with { PrinterTransport = "bluetooth" }, ct.Token);
        if (printer.ActiveTransport != "bluetooth") throw new Exception("Manual BLE selection failed");
        Console.WriteLine("PASS actual manual BLE connection and fresh media/battery");
        await printer.ConnectAsync(original with { PrinterTransport = "auto" }, ct.Token);
        if (printer.ActiveTransport != "usb") throw new Exception("USB restoration failed");
        Console.WriteLine("PASS actual BLE release and USB restoration");
    }
    public static Task Ui()
    {
        // Real form and transport; isolated configuration and no relay polling.
        var thread = new Thread(() =>
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
            var local = new LocalStore(Path.Combine(Path.GetTempPath(), "ps033-ui-" + Guid.NewGuid()));
            var original = new LocalStore().LoadSettings();
            var settings = new AppSettings { Server = "https://relay.example.com/", PrinterAddress = original.PrinterAddress, PrinterName = "Paperang_P2", StartAtLogin = false };
            using var printer = new PreferredPrinter(new UsbPrinter(), new BlePrinter());
            var engine = new RelayEngine(printer, new UiRenderer(), local);
            printer.ConnectAsync(settings, default).GetAwaiter().GetResult();
            using var form = new SettingsForm(settings, engine, async (value, _) => {
                printer.Disconnect(); await printer.ConnectAsync(value, default); local.SaveSettings(value);
            }, (_, _) => { }, (value, _) => { local.SaveDraft(value); return "验证配置已保存到隔离目录，不连接中转、不自动打印。"; }, local);
            using var timer = new System.Windows.Forms.Timer { Interval = 250 };
            timer.Tick += (_, _) => form.UpdateStatus(); timer.Start();
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        return Task.CompletedTask;
    }
    sealed class UiRenderer : IRasterizer { public Raster Render(RelayJob j, AppSettings s) => throw new NotSupportedException(); }
    public static async Task Run(Action<bool, string> check, AppSettings settings)
    {
        var decoder = new FrameDecoder(UsbP2Session.Seed, true);
        var data = Convert.FromHexString("020D0002020D0002001100B176FA1E03");
        check(decoder.Push(data).Single().Payload.SequenceEqual(new byte[] { 17, 0 }), "USB CRC and corrupt partial header resync");
        using (var link = new UsbFake())
        using (var session = new UsbP2Session(link))
        {
            await session.ConnectAsync(default);
            check(session.Ready && session.Status.Contains("100%"), "USB model/density/status/battery handshake without BLE key registration");
            await session.PrintAsync(new Raster(56, new byte[4032]), settings, default);
            check(link.Images.Select(x => x.Index).SequenceEqual(new byte[] { 3, 2, 1, 0 }) && link.Images.Sum(x => x.Payload.Length) == 4032,
                "USB whole-row raster countdown and exact payload");
            check(link.Feed.SequenceEqual(new byte[] { 150, 0 }) && !link.Commands.Contains(24), "USB feed150 and no CRC-key/FF03 handshake");
            foreach (ushort media in new ushort[] { 1, 16, 17 })
            {
                link.Media = media; int before = link.Images.Count;
                try { await session.PrintAsync(new Raster(1, new byte[72]), settings, default); throw new Exception("media guard missing"); }
                catch (InvalidOperationException) { }
                check(link.Images.Count == before, "USB no raster writes with paper/cover fault " + media);
            }
            link.Media = 0; link.WrongDensity = true;
            try { await session.PrintAsync(new Raster(1, new byte[72]), settings, default); throw new Exception("density guard missing"); }
            catch (InvalidOperationException) { }
            check(link.Images.Count == 4, "USB density mismatch prevents printing");
        }
        ChoiceFake usb = new(), ble = new();
        using (var preferred = new PreferredPrinter(usb, ble))
        {
            await preferred.ConnectAsync(settings, default);
            check(preferred.ActiveTransport == "usb" && ble.Connects == 0 && ble.Disconnects > 0, "USB priority suppresses Bluetooth connection");
            usb.FailPrint = true;
            try { await preferred.PrintAsync(new Raster(1, new byte[72]), settings, default); throw new Exception("expected write failure"); }
            catch (IOException) { }
            check(usb.Prints == 1 && ble.Prints == 0 && ble.Connects == 0, "uncertain USB print is never replayed on Bluetooth");
            await preferred.ConnectAsync(settings with { PrinterTransport = "bluetooth" }, default);
            check(preferred.ActiveTransport == "bluetooth" && usb.Connects == 1 && ble.Connects == 1 && !usb.Ready, "manual Bluetooth mode releases USB and skips USB probe");
            usb.FailConnect = true;
            await preferred.ConnectAsync(settings, default);
            check(preferred.ActiveTransport == "bluetooth" && ble.Connects == 2, "USB connection failure falls back to remembered Bluetooth");
        }
        usb = new ChoiceFake { CancelConnect = true }; ble = new ChoiceFake();
        using (var preferred = new PreferredPrinter(usb, ble))
        {
            try { await preferred.ConnectAsync(settings, default); throw new Exception("expected cancellation"); } catch (OperationCanceledException) { }
            check(ble.Connects == 0, "connection cancellation never starts Bluetooth fallback");
        }
        var old = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{}", RelayClient.Json)!;
        check(old.PrinterTransport == "auto", "old schema1 configuration defaults to USB priority");
        using (var link = new UsbFake(0))
        using (var session = new UsbP2Session(link))
        {
            await session.ConnectAsync(default);
            check(session.Ready && !link.Commands.Contains(24), "USB detects BLE-persisted key zero using read-only identity query");
        }
    }

    public static async Task Hardware(bool print, string? attempt)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var settings = new AppSettings { Density = 100, Feed = 150 };
        using var printer = new PreferredPrinter(new UsbPrinter(), new BlePrinter());
        await printer.ConnectAsync(settings, cts.Token);
        if (printer.ActiveTransport != "usb") throw new Exception("Expected actual USB");
        Console.WriteLine(printer.ActiveTransport + " · " + printer.Status);
        await printer.RefreshAsync(cts.Token);
        if (print)
        {
            if (attempt == null) throw new ArgumentException("Attempt path required");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(attempt))!);
            using (var file = new FileStream(attempt, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(System.Text.Encoding.UTF8.GetBytes("{\"state\":\"unknown\",\"transport\":\"usb\",\"version\":\"" + AppVersion.Current + "\"}")); file.Flush(true); }
            var raster = new byte[4032];
            for (int y = 0; y < 56; y++) for (int x = 0; x < 72; x++)
                raster[y * 72 + x] = (byte)(x is 0 or 71 || x == 4 + (y / 14) * 5 ? 255 : 0);
            await printer.PrintAsync(new Raster(56, raster), settings, cts.Token);
            File.WriteAllText(attempt, "{\"state\":\"sent\",\"transport\":\"usb\",\"paperConfirmed\":null}");
            Console.WriteLine("USB pattern sent once; paper confirmation remains separate.");
        }
        printer.Disconnect();
        await printer.ConnectAsync(settings, cts.Token);
        Console.WriteLine("USB reconnect: " + printer.Status);
    }

    sealed class ChoiceFake : IPrinter
    {
        public int Connects, Disconnects, Prints;
        public bool FailConnect, FailPrint, CancelConnect;
        public bool Ready { get; private set; }
        public string Status => "fake";
        public double Progress => 0;
        public Task ConnectAsync(AppSettings settings, CancellationToken ct)
        { Connects++; if (CancelConnect) throw new OperationCanceledException(); if (FailConnect) throw new IOException("missing"); Ready = true; return Task.CompletedTask; }
        public Task PrintAsync(Raster r, AppSettings s, CancellationToken ct) { Prints++; if (FailPrint) throw new IOException("uncertain"); return Task.CompletedTask; }
        public Task RefreshAsync(CancellationToken ct) => Task.CompletedTask;
        public void Disconnect() { Disconnects++; Ready = false; }
        public void Dispose() => Disconnect();
    }
    sealed class UsbFake(uint seed = UsbP2Session.Seed) : IUsbLink
    {
        readonly Queue<byte[]> replies = new();
        readonly FrameDecoder decoder = new(seed);
        public List<Frame> Images = [];
        public List<byte> Commands = [];
        public byte[] Feed = [];
        public ushort Media;
        public bool WrongDensity;
        byte density = 100;
        public bool Connected => true;
        public Task<byte[]> ReadAsync(TimeSpan timeout, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(replies.Count == 0 ? Array.Empty<byte>() : replies.Dequeue()); }
        public Task WriteAsync(byte[] data, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var decoded = decoder.Push(data).ToArray();
            if (decoded.Length == 0) return Task.CompletedTask;
            var f = decoded.Single(); Commands.Add(f.Command);
            byte[]? payload = f.Command switch { 6 => System.Text.Encoding.ASCII.GetBytes("Paperang_P2"), 16 => [100], 12 => [(byte)Media, (byte)(Media >> 8)], 28 => [WrongDensity ? (byte)2 : density], _ => null };
            if (f.Command == 0) Images.Add(f);
            else if (payload != null) replies.Enqueue(Wire.Encode((byte)(f.Command + 1), payload, seed));
            else
            {
                if (f.Command == 25) density = f.Payload.Single();
                if (f.Command == 26) Feed = f.Payload;
                replies.Enqueue(Wire.Encode(f.Command, [0], seed));
            }
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }
}
