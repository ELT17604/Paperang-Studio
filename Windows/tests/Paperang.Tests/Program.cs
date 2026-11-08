using Paperang.Core;
using Paperang.WinLite;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class Tests
{
    private static int count;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    static async Task Until(Func<bool> condition, int ms = 12000) { var end = DateTime.UtcNow.AddMilliseconds(ms); while (!condition()) { if (DateTime.UtcNow > end) throw new TimeoutException("Test condition timed out"); await Task.Delay(20); } }
    static AppSettings Settings => new() { AuthenticationMode = "legacy", Server = "http://127.0.0.1:8177", DeviceKey = "psr_test", PrinterAddress = "112233445566" };
    static RelayJob Job => new() { Id = new string('a', 40), Text = "Hello 你好，纸条！", Created = 1793894400 };
    [STAThread]
    static async Task Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--admin-ui") {
                System.Windows.Forms.Application.EnableVisualStyles();
                using var form = new AdminForm(Settings); System.Windows.Forms.Application.Run(form); return;
            }
            if (args.Length == 2 && args[0] == "--admin-test") { await AdminTest(args[1]); return; }
            if (args.Length > 0 && args[0] == "--ble") { await Ble(args.Contains("--print")); return; }
            if (args.Length > 0 && args[0] == "--preferences")
            {
                var s = new LocalStore().LoadSettings();
                var snapshot = JsonSerializer.Serialize(new { s.Style, s.TemplateId, s.Templates, s.Density, s.Feed, s.IncludeId, s.IncludeTime, s.IncludeSender });
                Console.WriteLine(JsonSerializer.Serialize(new { version = AppVersion.Current, style = s.Style, templateCount = s.Templates.Count, printFingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))), continuousReconnect = s.KeepReconnecting, startupEnabled = StartupRegistration.Enabled }));
                return;
            }
            if (args.Length == 2 && args[0] == "--live") { await Live(args[1]); return; }
            if (args.Length > 0 && args[0] == "--minute-retry") { await MinuteRetry(); return; }
            Check(Wire.Crc(Encoding.ASCII.GetBytes("123456789")) == 0xcbf43926, "standard CRC32 vector");
            Check(Convert.ToHexString(Wire.Encode(24, [0x21, 0x95, 0x76, 0x35], 0x35769521)) == "0218000400219576351CDF442103", "handshake golden frame from independent Python zlib");
            var good = Wire.Encode(13, [8, 0]); var decoder = new FrameDecoder();
            Check(!decoder.Push(good[..6]).Any() && decoder.Push(good[6..]).Single().Payload.SequenceEqual(new byte[] { 8, 0 }), "fragmented notifications");
            var bad = (byte[])good.Clone(); bad[5] ^= 1;
            Check(new FrameDecoder().Push(new byte[] { 9, 9 }.Concat(bad).Concat(good).ToArray()).Count() == 1, "bad CRC rejected and stream resynchronized");
            foreach (string url in new[] { "http://example.com", "https://user@example.com", "https://example.com/path", "https://example.com?x=1" })
            { try { (Settings with { Server = url }).Validate(); throw new Exception("accepted unsafe URL"); } catch (ArgumentException) { count++; } }
            using (var link = new FakeBle())
            using (var session = new P2Session(link))
            {
                link.Prime(); await session.HandshakeAsync(default);
                Check(session.Ready, "P2 model and BLE handshake");
                await session.PrintAsync(new Raster(30, new byte[2160]), Settings, default);
                Check(link.Bitmaps.Select(x => x.Index).SequenceEqual(new byte[] { 0, 1, 2 }) && link.Bitmaps.SelectMany(x => x.Payload).Count() == 2160, "1008 byte image payloads, increasing index and exact bytes");
                Check(link.Feed.SequenceEqual(new byte[] { 150, 0 }), "150 line little-endian feed");
                link.Media = 0x10; int before = link.Bitmaps.Count;
                try { await session.PrintAsync(new Raster(1, new byte[72]), Settings, default); throw new Exception("printed with cover open"); } catch (InvalidOperationException) { }
                Check(link.Bitmaps.Count == before, "cover-open blocks all image writes");
                link.Media = 1;
                try { await session.PrintAsync(new Raster(1, new byte[72]), Settings, default); throw new Exception("printed without paper"); } catch (InvalidOperationException) { }
                Check(link.Bitmaps.Count == before, "paper-out blocks image writes");
                link.Media = 8; link.WrongDensity = true;
                try { await session.PrintAsync(new Raster(1, new byte[72]), Settings, default); throw new Exception("printed at wrong density"); } catch (InvalidOperationException) { }
                Check(link.Bitmaps.Count == before, "density read-back mismatch blocks image writes");
            }
            using (var link = new FakeBle { Model = "Paperang_P1" })
            using (var session = new P2Session(link))
            {
                link.Prime(); try { await session.HandshakeAsync(default); throw new Exception("accepted P1"); } catch (InvalidOperationException) { }
                Check(!session.Ready, "non-P2 model rejected");
            }
            using (var link = new FakeBle())
            using (var session = new P2Session(link))
            {
                using var cts = new CancellationTokenSource(80);
                try { await session.HandshakeAsync(cts.Token); throw new Exception("sent without credits"); } catch (OperationCanceledException) { }
                Check(link.WriteCount == 0, "no BLE writes without FF03 credits; cancellation remains responsive");
            }
            Raster image = new(1, new byte[72]);
            var content = JsonSerializer.SerializeToElement(new { type = "raster1", schemaVersion = 1, width = 576, height = 1, bitOrder = "msb-first", blackIsOne = true, encoding = "base64", data = Convert.ToBase64String(image.Bytes), sha256 = Convert.ToHexStringLower(SHA256.HashData(image.Bytes)) });
            Check((Job with { Content = content }).Image()!.Bytes.Length == 72, "raster1 checksum and size validation");
            var corrupt = JsonSerializer.SerializeToElement(new { type = "raster1", schemaVersion = 1, width = 576, height = 1, bitOrder = "msb-first", blackIsOne = true, encoding = "base64", data = Convert.ToBase64String(image.Bytes), sha256 = new string('0', 64) });
            try { (Job with { Content = corrupt }).Image(); throw new Exception("bad SHA accepted"); } catch (InvalidDataException) { count++; }
            var raster = new NoteRenderer().Render(Job, Settings);
            Check(raster.Height is > 1 and <= 1400 && raster.Bytes.Any(x => x != 0), "Windows GDI Chinese text produces packed black pixels");
            try { new NoteRenderer().Render(Job with { Text = new string('汉', 10000) }, Settings); throw new Exception("long note cropped"); } catch (InvalidDataException) { count++; }
            TemplateCases();
            await EngineCases();
            PersistenceCases();
            await RetryCases();
            // Real DPAPI storage is isolated to a temporary name via the pure encryption round-trip helper test below.
            var protect = typeof(LocalStore).GetMethod("Protect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            byte[] secret = Encoding.UTF8.GetBytes("psr_not_plaintext");
            byte[] encrypted = (byte[])protect.Invoke(null, [secret, true])!;
            Check(!Encoding.UTF8.GetString(encrypted).Contains("psr_") && ((byte[])protect.Invoke(null, [encrypted, false])!).SequenceEqual(secret), "Windows user DPAPI encryption round-trip");
            Console.WriteLine($"ALL PASSED: {count}");
        }
        catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
    }
    private static async Task AdminTest(string path)
    {
        var config = JsonDocument.Parse(await File.ReadAllTextAsync(path)).RootElement;
        var settings = Settings with { Server = config.GetProperty("server").GetString()!, DeviceKey = config.GetProperty("key").GetString()! };
        using var login = new RelayClient(settings);
        var session = await login.RequestAsync(HttpMethod.Post, "v2/auth/login", new { credential = settings.DeviceKey }, default,
            new Dictionary<string,string> { ["X-Paperang-Native"] = "1" });
        using var owner = new RelayClient(settings with { DeviceKey = session.GetProperty("token").GetString()! });
        Check((await owner.Get("v2/auth/me", default)).GetProperty("role").GetString() == "C", "Windows C native login, session token validation and permissions");
        var created = await owner.Post("v2/admin/keys", new { name = "Windows integration", role = "B", kind = "passphrase" }, default);
        var uid = created.GetProperty("user_id").GetString();
        var id = created.GetProperty("id").GetString();
        var rotated = await owner.Post($"v2/admin/keys/{id}/rotate", new { }, default);
        Check(rotated.GetProperty("user_id").GetString() == uid, "Windows credential rotation preserves stable user identity");
        using var rsa = RSA.Create(3072);
        var pub = await owner.Post("v2/admin/public-keys", new { name = "Windows RSA owner", public_key = rsa.ExportSubjectPublicKeyInfoPem() }, default);
        var challenge = await login.Post("v2/auth/challenge", new { id = pub.GetProperty("id").GetString() }, default);
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(challenge.GetProperty("message").GetString()!), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var proof = await login.Post("v2/auth/prove", new { challenge = challenge.GetProperty("challenge").GetString(), signature = Convert.ToBase64String(signature) }, default);
        Check(proof.GetProperty("role").GetString() == "C", "Windows RSA-PSS signature interoperates with Python verification");
        var page = await owner.Post("v2/admin/jobs/export", new { user_id = "" }, default);
        Check(page.GetProperty("jobs").GetArrayLength() == 2 && page.GetProperty("jobs")[0].TryGetProperty("metadata", out _), "Windows C export returns text/image jobs and delivery metadata");
        string ownerFile = Path.Combine(Path.GetTempPath(), "paperang-owner-" + Guid.NewGuid() + ".json");
        try {
            AdminForm.WritePrivate(ownerFile, JsonSerializer.Serialize(new { server = settings.Server, id = pub.GetProperty("id").GetString(), privateKeyPem = rsa.ExportPkcs8PrivateKeyPem() }));
            var ownerSettings = settings with { AuthenticationMode = "owner", OwnerKeyFile = ownerFile, DeviceKey = "" };
            using var nativeOwner = new RelayClient(ownerSettings);
            Check((await nativeOwner.Get("v2/auth/me", default)).GetProperty("role").GetString() == "C", "configured owner file renews native proof without saved bearer");
            await nativeOwner.Post("v2/device/status", new { status = new { connected = true } }, default);
            Check((await nativeOwner.Get("v2/device/jobs", default)).GetProperty("jobs").ValueKind == JsonValueKind.Array, "same proven owner identity accesses scoped execution and management");
            Check(RelayEngine.CredentialFingerprint(ownerSettings) == RelayEngine.CredentialFingerprint(ownerSettings), "owner journal affinity uses stable public key identity");
            var blockedHandler = new BlockedAuthentication();
            using var blockedHttp = new HttpClient(blockedHandler) { BaseAddress = settings.Validate() };
            using var cancellation = new CancellationTokenSource();
            var authenticator = new OwnerAuthentication(ownerFile, settings.Validate());
            var signing = authenticator.GetTokenAsync(blockedHttp, cancellation.Token);
            await Until(() => blockedHandler.Started);
            authenticator.Dispose(); cancellation.Cancel();
            try { await signing; throw new Exception("cancelled authentication completed"); }
            catch (OperationCanceledException) { Check(true, "closing owner client during authentication cancels without disposed-gate fault"); }

        } finally { File.Delete(ownerFile); }
        string privateFile = Path.Combine(Path.GetTempPath(), "paperang-acl-" + Guid.NewGuid() + ".json");
        try { AdminForm.WritePrivate(privateFile, "fixture"); Check(File.ReadAllText(privateFile) == "fixture" && new FileInfo(privateFile).GetAccessControl().AreAccessRulesProtected, "Windows owner file disables inherited ACL before writing secret"); }
        finally { File.Delete(privateFile); }
    }
    private static async Task MinuteRetry()
    {
        var handler = new OutageHandler();
        var engine = new RelayEngine(new FakePrinter(), new FakeRenderer(), new MemoryJournal(), s => new RelayClient(s, handler));
        await engine.StartAsync(Settings with { PrinterAddress = "" });
        await Until(() => engine.NextRetryAt != null, 20000);
        int before = handler.Requests;
        var watch = System.Diagnostics.Stopwatch.StartNew(); handler.FailuresLeft = -1;
        await Until(() => engine.Online, 70000);
        Check(watch.Elapsed.TotalSeconds >= 58 && before == 6, $"real 60 second cooldown recovers unattended after network restoration ({watch.Elapsed.TotalSeconds:F1}s)");
        await engine.StopAsync();
    }
    private static void PersistenceCases()
    {
        string directory = Path.Combine(Path.GetTempPath(), "paperang-fix1-test-" + Guid.NewGuid());
        try
        {
            var store = new LocalStore(directory);
            var template = RelayTemplate.Preset("访客纪念") with { Name = "已保存的自定义模板" };
            var value = Settings with { Style = "自定义", Templates = [template], TemplateId = template.Id, Density = 73, Feed = 127, IncludeId = true };
            store.SaveSettings(value);
            var restored = new LocalStore(directory).LoadSettings();
            Check(restored.TemplateId == template.Id && restored.Style == "自定义" && restored.Templates[0].Name == template.Name && restored.Density == 73 && restored.Feed == 127, "process restart restores selected custom template and printing preferences");
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory, "settings.dat"))).Contains("psr_test"), "saved configuration contains no plaintext device key");
            var changed = restored with { Style = "小票", Feed = 189 };
            var merged = Settings.WithPrintingFrom(changed);
            Check(merged.Server == Settings.Server && merged.DeviceKey == Settings.DeviceKey && merged.Feed == 189 && merged.TemplateId == template.Id, "print autosave preserves connection credentials and selected template");
            var legacy = JsonSerializer.Deserialize<AppSettings>("{}", RelayClient.Json)!;
            Check(legacy.KeepReconnecting && RelayTiming.Default.RetryCooldown == TimeSpan.FromMinutes(1), "old settings default to continuous reconnect and exact 60 second cooldown");
            Parallel.For(0, 80, i => { store.SaveSettings(value with { Feed = i }); store.Save(new PendingResult("http://localhost/", "fixture", new string('a', 40), "fixture")); });
            Check(store.LoadSettings().TemplateId == template.Id && store.Load()?.Claim == "fixture" && !Directory.GetFiles(directory, "*.tmp").Any(), "concurrent settings and claim writes are atomic and bounded");
            Check(StartupRegistration.BuildCommand(Environment.ProcessPath!) == "\"" + Environment.ProcessPath + "\" --background", "startup command uses existing absolute quoted executable and background flag");
        }
        finally { Directory.Delete(directory, true); }
    }
    private static async Task RetryCases()
    {
        var fast = new RelayTiming(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(40));
        var handler = new OutageHandler { FailuresLeft = 11 };
        var engine = new RelayEngine(new FakePrinter(), new FakeRenderer(), new MemoryJournal(), s => new RelayClient(s, handler), fast);
        int cooldowns = 0; engine.Changed += () => { if (engine.NextRetryAt != null) cooldowns++; };
        await engine.StartAsync(Settings with { PrinterAddress = "" }); await Until(() => engine.Online);
        Check(handler.Requests >= 12 && cooldowns == 2, "six initial failures then five-attempt batches recover automatically after two cooldowns");
        await engine.StopAsync();

        handler = new OutageHandler();
        engine = new(new FakePrinter(), new FakeRenderer(), new MemoryJournal(), s => new RelayClient(s, handler), fast);
        await engine.StartAsync(Settings with { KeepReconnecting = false });
        await Until(() => !engine.Running);
        Check(handler.Requests == 6 && engine.NextRetryAt == null, "disabling continuous retry stops after original five retries");
        await engine.StopAsync();

        handler = new OutageHandler();
        engine = new(new FakePrinter(), new FakeRenderer(), new MemoryJournal(), s => new RelayClient(s, handler), fast with { RetryCooldown = TimeSpan.FromMinutes(1) });
        await engine.StartAsync(Settings); await Until(() => engine.NextRetryAt != null);
        int requests = handler.Requests;
        Check((engine.NextRetryAt!.Value - DateTimeOffset.UtcNow).TotalSeconds > 58, "production-length cooldown has no polling busy loop");
        var watch = System.Diagnostics.Stopwatch.StartNew(); await engine.StopAsync();
        Check(watch.Elapsed < TimeSpan.FromSeconds(1) && handler.Requests == requests, "stop cancels a one-minute cooldown immediately");

        GC.Collect(); GC.WaitForPendingFinalizers(); long before = GC.GetTotalMemory(true);
        handler = new OutageHandler();
        engine = new(new FakePrinter(), new FakeRenderer(), new MemoryJournal(), s => new RelayClient(s, handler), new(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
        await engine.StartAsync(Settings with { PrinterAddress = "" }); await Until(() => handler.Requests >= 1200, 20000);
        await engine.StopAsync();
        long after = GC.GetTotalMemory(true);
        Check(handler.MaxConcurrent == 1 && after - before < 16 * 1024 * 1024, $"1200 offline requests retain bounded memory and single-flight IO (delta {after - before} bytes)");
        var tasks = Enumerable.Range(0, 12).Select(async i => { await engine.StartAsync(Settings with { PrinterAddress = "" }); await engine.StopAsync(); });
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
        Check(!engine.Running && handler.MaxConcurrent == 1, "overlapping start/stop requests finish without duplicate loops or deadlock");
    }
    sealed class OutageHandler : HttpMessageHandler
    {
        public int FailuresLeft = int.MaxValue, Requests, MaxConcurrent;
        private int active;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            int now = Interlocked.Increment(ref active); MaxConcurrent = Math.Max(MaxConcurrent, now);
            try
            {
                Interlocked.Increment(ref Requests);
                if (Interlocked.Decrement(ref FailuresLeft) >= 0) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/jobs") ? "{\"jobs\":[]}" : "{}") });
            }
            finally { Interlocked.Decrement(ref active); }
        }
        protected override void Dispose(bool disposing) { }
    }
    private static async Task Ble(bool print)
    {
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var settings = new LocalStore().LoadSettings();
        if (settings.PrinterAddress.Length == 0)
        {
            var devices = await BlePrinter.ScanAsync(false, ct.Token);
            Console.WriteLine($"Scanned {devices.Length} candidates");
            if (devices.Length != 1) throw new InvalidOperationException("Select one P2 in settings before diagnostics");
            settings = settings with { PrinterAddress = devices[0].Address.ToString("X12"), PrinterName = devices[0].Name };
        }
        using var printer = new BlePrinter(message => Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + message));
        await printer.ConnectAsync(settings, ct.Token);
        Console.WriteLine("CONNECTED " + printer.Status);
        for (int i = 1; i <= 2; i++)
        {
            printer.Disconnect();
            await Task.Delay(1000, ct.Token);
            await printer.ConnectAsync(settings, ct.Token);
            Console.WriteLine($"RECONNECTED {i}/2 " + printer.Status);
        }
        if (print)
        {
            var raster = new NoteRenderer().Render(Job with { Text = "Paperang Windows BLE test\n蓝牙连接与中文打印测试\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }, settings with { Style = "便签" });
            await printer.PrintAsync(raster, settings, ct.Token);
            Console.WriteLine("PRINT DATA SENT; physical output requires user confirmation");
        }
    }
    private static void TemplateCases()
    {
        var renderer = new NoteRenderer();
        var template = RelayTemplate.Preset("便签");
        var json = JsonSerializer.Serialize(template, RelayClient.Json);
        var roundtrip = JsonSerializer.Deserialize<RelayTemplate>(json, RelayClient.Json)!; roundtrip.Validate();
        using var document = JsonDocument.Parse(json);
        Check(document.RootElement.GetProperty("blocks")[0].GetProperty("font").GetString() == "PingFang SC" && roundtrip.Blocks.Count == 4, "Swift Codable v1 template keys and fonts preserved");
        var job = TemplateEditor.SampleJob("{{ip}} — 中文说明", true);
        var browserBlock = new RelayBlock { Kind = "浏览器信息" };
        var anonymousJob = job with { Metadata = new() { ["userAgent"] = JsonSerializer.SerializeToElement("Firefox/132.0"), ["language"] = JsonSerializer.SerializeToElement("zh-CN") } };
        Check(browserBlock.Value(anonymousJob) == "Firefox/132.0 · language=zh-CN", "anonymous browser block falls back to delivered UA and language on older relays");
        Check(browserBlock.Value(job).Contains("platform=Sample device"), "structured browser metadata remains preferred");
        Check(browserBlock.Value(job with { Metadata = new() }) == "浏览器信息未提供", "missing browser data is explicit rather than an empty block");
        var block = new RelayBlock { Kind = "自定义文字", Text = "{{text}} / {{id}} / {{sender}}" };
        Check(block.Value(job).StartsWith("{{ip}} — 中文说明 / A1B2-C3D4-E5F6 / Sample visitor"), "template variables expand once; visitor text never expanded recursively");
        template = template with { Blocks = [new RelayBlock { Kind = "标题", Text = "TEST", Gap = 5 }, new RelayBlock { Kind = "正文", Margin = 0, Gap = 7 }, new RelayBlock { Kind = "投稿身份", Gap = 9 }] };
        var parts = renderer.Parts(job, template);
        var image = job.Image()!;
        Check(parts.Count == 3 && parts[1].Raster.Bytes.Take(image.Bytes.Length).SequenceEqual(image.Bytes), "custom body places exact image before caption; margin zero preserves pixels");
        var settings = Settings with { Style = "自定义", Templates = [template], TemplateId = template.Id };
        var final = renderer.Render(job, settings);
        Check(final.Bytes.SequenceEqual(Raster.Stack(parts.Select(x => (x.Raster, x.Gap))).Bytes), "editor block preview and real print share identical composition");
        int offset = (parts[0].Gap + parts[0].Raster.Height + parts[1].Gap) * 72;
        Check(final.Bytes.AsSpan(offset, image.Bytes.Length).SequenceEqual(image.Bytes), "title/body/footer order and upper whitespace preserved");
        var noBody = template with { Blocks = template.Blocks.Select(x => x.Kind == "正文" ? x with { Enabled = false } : x).ToList() };
        Check(renderer.Parts(job, noBody).Count == 2, "disabling body removes both drawing and caption");
        var fit = image.Fitting(120);
        Check(fit.Height == (image.Height * 336 + 575) / 576 && fit.Bytes.Where((_, i) => i % 72 < 15 || i % 72 >= 57).All(x => x == 0), "image fitting uses symmetric white margins and proportional height");
        foreach (var preset in RelayTemplate.Defaults()) { preset.Validate(); _ = Raster.Stack(renderer.Parts(job, preset).Select(x => (x.Raster, x.Gap))); }
        Check(true, "all four macOS presets render text and drawing");
        try { (template with { Blocks = [block with { Size = 97 }] }).Validate(); throw new Exception("oversized font accepted"); } catch (InvalidDataException) { count++; }
        try { Raster.Stack([(new Raster(1400, new byte[100800]), 1)]); throw new Exception("oversize composition accepted"); } catch (InvalidDataException) { count++; }
    }
    private static async Task EngineCases()
    {
        var journal = new MemoryJournal(); var handler = new RelayHandler(Job); var printer = new FakePrinter();
        var engine = new RelayEngine(printer, new FakeRenderer(), journal, s => new RelayClient(s, handler));
        await engine.StartAsync(Settings with { AutoAllowPrint = false });
        await Until(() => engine.Online);
        Check(handler.Claims == 0, "paused relay polls without claiming");
        engine.SetAllowed(true);
        await Until(() => journal.Value == null && handler.Sent == 1);
        Check(printer.Prints == 1 && handler.Claims == 1 && handler.HeadersValid, "authenticated capability header and exactly-once claim/print/result");
        await engine.StopAsync();

        journal = new MemoryJournal { Value = new(Settings.Validate().AbsoluteUri, RelayEngine.Fingerprint(Settings.DeviceKey), Job.Id, "oldclaim") };
        handler = new RelayHandler(Job) { State = "printing" }; printer = new FakePrinter();
        engine = new(printer, new FakeRenderer(), journal, s => new RelayClient(s, handler));
        await engine.StartAsync(Settings); await Until(() => handler.Unknown == 1);
        Check(printer.Prints == 0 && journal.Value == null, "restart reports old claim unknown without reprinting");
        await engine.StopAsync();

        journal = new MemoryJournal(); handler = new RelayHandler(Job) { FailResults = 2 }; printer = new FakePrinter();
        engine = new(printer, new FakeRenderer(), journal, s => new RelayClient(s, handler));
        await engine.StartAsync(Settings); await Until(() => handler.Sent == 1);
        Check(printer.Prints == 1 && handler.Claims == 1, "lost final reports retried without second print");
        await engine.StopAsync();

        journal = new MemoryJournal(); handler = new RelayHandler(Job); printer = new FakePrinter { Wait = true };
        engine = new(printer, new FakeRenderer(), journal, s => new RelayClient(s, handler));
        await engine.StartAsync(Settings); await Until(() => engine.Busy);
        Check(journal.Value?.State == "unknown", "claim is durable before physical send");
        engine.SetAllowed(false);
        await Until(() => handler.PrintingReports >= 2);
        Check(engine.Busy && handler.PrintingReports >= 2, "pause preserves active send; printing claim heartbeats continue");
        await engine.StopAsync();
        Check(journal.Value?.State == "unknown" && !engine.Allowed, "shutdown mid-send leaves unknown and pauses");

        journal = new MemoryJournal { Value = new(Settings.Validate().AbsoluteUri, RelayEngine.Fingerprint(Settings.DeviceKey), Job.Id, "x") };
        engine = new(new FakePrinter(), new FakeRenderer(), journal);
        try { await engine.StartAsync(Settings with { DeviceKey = "psr_other" }); throw new Exception("changed credential with pending claim"); } catch (InvalidOperationException) { count++; }
    }
    private static async Task Live(string path)
    {
        var config = JsonDocument.Parse(await File.ReadAllTextAsync(path)).RootElement;
        var settings = Settings with { Server = config.GetProperty("server").GetString()!, DeviceKey = config.GetProperty("key").GetString()! };
        var printer = new FakePrinter(); var journal = new MemoryJournal();
        var engine = new RelayEngine(printer, new NoteRenderer(), journal);
        engine.Changed += () => Console.WriteLine($"TRACE {engine.Message} | {engine.PrintMessage} | prints={printer.Prints}");
        await engine.StartAsync(settings);
        await Until(() => printer.Prints == 2 && journal.Value == null, 20000);
        await engine.StopAsync();
        Check(printer.Prints == 2, "original Python relay: text and raster1 end-to-end");
    }
    sealed class BlockedAuthentication : HttpMessageHandler
    {
        public bool Started;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Started = true; await Task.Delay(Timeout.Infinite, ct); return new(HttpStatusCode.OK); }
    }
    sealed class MemoryJournal : IJournal { public PendingResult? Value; public PendingResult? Load() => Value; public void Save(PendingResult? v) => Value = v; }
    sealed class FakeRenderer : IRasterizer { public Raster Render(RelayJob j, AppSettings s) => new(1, new byte[72]); }
    sealed class FakePrinter : IPrinter
    {
        public bool Ready { get; private set; } public string Status => "Fake P2"; public double Progress => 0;
        public int Prints; public bool Wait;
        public Task ConnectAsync(AppSettings s, CancellationToken ct) { Ready = true; return Task.CompletedTask; }
        public async Task PrintAsync(Raster r, AppSettings s, CancellationToken ct) { Prints++; if (Wait) await Task.Delay(Timeout.Infinite, ct); }
        public Task RefreshAsync(CancellationToken ct) => Task.CompletedTask;
        public void Disconnect() => Ready = false; public void Dispose() { }
    }
    sealed class RelayHandler(RelayJob job) : HttpMessageHandler
    {
        public int Claims, Sent, Unknown, FailResults, PrintingReports; public bool HeadersValid = true; public string State = "queued";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            HeadersValid &= req.Headers.Authorization?.Parameter == "psr_test" && req.Headers.GetValues("X-Paperang-Content-Types").Single() == "text,raster1";
            if (req.Content != null) HeadersValid &= req.Content.Headers.ContentLength > 0;
            string p = req.RequestUri!.AbsolutePath;
            object result = new { accepted = true };
            if (p.EndsWith("/claim")) { Claims++; State = "claimed"; result = new { claim = "claim" }; }
            else if (p.EndsWith("/result"))
            {
                var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct)).RootElement;
                string state = body.GetProperty("state").GetString()!;
                if (state == "sent" && FailResults-- > 0) return new(HttpStatusCode.ServiceUnavailable);
                State = state; if (state == "sent") Sent++; if (state == "unknown") Unknown++; if (state == "printing") PrintingReports++;
            }
            else if (p == "/v2/device/jobs") result = new { jobs = new[] { job with { State = State, Text = "", Metadata = [], Truncated = true } } };
            else if (p == "/v2/device/jobs/" + job.Id) {
                HeadersValid &= Claims > 0 && req.Headers.TryGetValues("X-Job-Claim", out var claims) && claims.Single() == "claim";
                result = job with { State = State };
            }
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(result, RelayClient.Json)) };
        }
        protected override void Dispose(bool disposing) { } // factory reuses the fixture
    }
    sealed class FakeBle : IBleLink
    {
        public event Action<byte[]>? Data; public event Action<byte[]>? Flow;
        public bool Connected => true; public int MaxWrite => 20;
        public string Model = "Paperang_P2"; public ushort Media = 8; public bool WrongDensity;
        public int WriteCount; public List<Frame> Bitmaps = []; public byte[] Feed = []; private byte density = 100;
        private readonly List<byte> buffer = [];
        public void Prime() => Flow?.Invoke([1, 1]);
        public Task WriteAsync(byte[] bytes, CancellationToken ct)
        {
            WriteCount++; buffer.AddRange(bytes); Flow?.Invoke([1, 1]);
            if (buffer.Count < 10) return Task.CompletedTask;
            int n = buffer[3] | buffer[4] << 8; if (buffer.Count < n + 10) return Task.CompletedTask;
            byte command = buffer[1], index = buffer[2]; byte[] payload = buffer.GetRange(5, n).ToArray();
            uint crc = BitConverter.ToUInt32(buffer.GetRange(5 + n, 4).ToArray());
            if (crc != Wire.Crc(payload, command == 24 ? 0x35769521u : 0)) throw new Exception("CRC mismatch");
            buffer.RemoveRange(0, n + 10);
            if (command == 0) { Bitmaps.Add(new(command, index, payload)); return Task.CompletedTask; }
            if (command == 25) density = payload[0]; if (command == 26) Feed = payload;
            byte[]? reply = command switch { 6 => Encoding.UTF8.GetBytes(Model), 16 => [80], 12 => [(byte)Media, (byte)(Media >> 8)], 28 => [WrongDensity ? (byte)0 : density], _ => null };
            byte[] frames = Wire.Encode(command, [0]);
            if (reply != null) frames = frames.Concat(Wire.Encode((byte)(command + 1), reply)).ToArray();
            Data?.Invoke(frames[..7]); Data?.Invoke(frames[7..]);
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }
}
