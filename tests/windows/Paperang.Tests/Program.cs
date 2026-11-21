global using Paperang.Windows;
using Paperang.Core;
using Paperang.WinLite;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class Tests
{
	private static int count;
	static void Check(bool ok, string name)
	{
		if (!ok)
			throw new Exception(name);
		count++;
		Console.WriteLine("PASS " + name);
	}
	static async Task Until(Func<bool> condition, int ms = 12000)
	{
		var end = DateTime.UtcNow.AddMilliseconds(ms);
		while (!condition())
		{
			if (DateTime.UtcNow > end)
				throw new TimeoutException("Test condition timed out");
			await Task.Delay(20);
		}
	}
	static AppSettings Settings => new() { Server = "http://127.0.0.1:8177", AuthenticationMode = "legacy",
	                                       DeviceKey = "psr_test", PrinterAddress = "112233445566" };
	static RelayJob Job => new() { Id = new string('a', 40), Text = "Hello 你好，纸条！", Created = 1793894400 };
	static async Task Main(string[] args)
	{
		try
		{
			if (args.SequenceEqual(new[] { "--transport-cycle" }))
			{
				await UsbCases.Cycle();
				return;
			}
			if (args.SequenceEqual(new[] { "--usb-ui" }))
			{
				await UsbCases.Ui();
				return;
			}
			if (args.SequenceEqual(new[] { "--usb" }))
			{
				await UsbCases.Hardware(false, null);
				return;
			}
			if (args.Length == 3 && args[0] == "--usb-print" && args[2] == "--confirm-print")
			{
				await UsbCases.Hardware(true, args[1]);
				return;
			}
			if (args.SequenceEqual(new[] { "--probe-production" }))
			{
				await ProbeProduction();
				return;
			}
			if (args.Length == 3 && args[0] == "--physical" && args[2] == "--confirm-print")
			{
				await Physical(args[1]);
				return;
			}
			if (args.Length > 0 && args[0] == "--ble")
			{
				await Ble();
				return;
			}
			if (args.Length == 2 && args[0] == "--integration")
			{
				await Integration(args[1]);
				return;
			}
			Check(Wire.Crc(Encoding.ASCII.GetBytes("123456789")) == 0xcbf43926, "standard CRC32 vector");
			Check(Convert.ToHexString(Wire.Encode(24, [0x21, 0x95, 0x76, 0x35], 0x35769521)) ==
			          "0218000400219576351CDF442103",
			      "handshake golden frame from independent Python zlib");
			Check(P2Command.All.Where(command => command.Kind == "查询").Select(command => command.Code).Order()
				.SequenceEqual(P2Queries.Responses.Keys.Order()), "engineering catalog query entries exactly match the supported GET whitelist");
			foreach (var (command, payload) in new (byte, byte[])[] { (25, [100]), (26, [0xd0, 7]), (30, [8, 7]), (39, [0, 0]), (20, [0]), (44, [1]), (27, [0]), (34, [0]) })
				P2Engineering.Validate(command, payload);
			foreach (var (command, payload) in new (byte, byte[])[] { (2, [0]), (3, [0]), (1, [0]), (25, [101]), (26, [0xd1, 7]), (27, [1]), (34, [0, 0]), (30, [0]) })
			{
				try { P2Engineering.Validate(command, payload); throw new Exception("Unsafe engineering write accepted"); }
				catch (ArgumentException) { }
			}
			Check(true, "engineering whitelist accepts Mac command payloads and rejects firmware, compressed data and invalid values");
			var good = Wire.Encode(13, [8, 0]);
			var decoder = new FrameDecoder();
			Check(!decoder.Push(good[..6]).Any() &&
			          decoder.Push(good[6..]).Single().Payload.SequenceEqual(new byte[] { 8, 0 }),
			      "fragmented notifications");
			var bad = (byte[])good.Clone();
			bad[5] ^= 1;
			Check(new FrameDecoder().Push(new byte[] { 9, 9 }.Concat(bad).Concat(good).ToArray()).Count() == 1,
			      "bad CRC rejected and stream resynchronized");
			foreach (string url in new[] { "http://example.com", "https://user@example.com", "https://example.com/path",
			                               "https://example.com?x=1" })
			{
				try
				{
					(Settings with { Server = url }).Validate();
					throw new Exception("accepted unsafe URL");
				}
				catch (ArgumentException)
				{
					count++;
				}
			}
			using (var link = new FakeBle()) using (var session = new P2Session(link))
			{
				var traces = new List<ProtocolTrace>();
				session.Trace = traces.Add;
				link.Prime();
				await session.HandshakeAsync(default);
				Check(session.Ready, "P2 model and BLE handshake");
				Check((await session.QueryAsync(8, default)).SequenceEqual(new byte[] { 1, 2, 3 }),
				      "GET 0x08 accepts the documented shared 0x0B response, not command plus one");
				int beforeQuery = link.WriteCount;
				try { await session.QueryAsync(25, default); throw new Exception("write command accepted as query"); }
				catch (ArgumentException) { }
				Check(link.WriteCount == beforeQuery, "read-only query API rejects a density write before transmission");
				await session.PrintAsync(new Raster(30, new byte[2160]), Settings, default);
				Check(link.Bitmaps.Select(x => x.Index).SequenceEqual(new byte[] { 0, 1, 2 }) &&
				          link.Bitmaps.SelectMany(x => x.Payload).Count() == 2160,
				      "1008 byte image payloads, increasing index and exact bytes");
				Check(link.Feed.SequenceEqual(new byte[] { 160, 0 }), "160 line little-endian feed");
				Check(traces.Any(trace => trace.Direction == "RX" && trace.Command == 7) &&
				      traces.Count(trace => trace.Direction == "TX" && trace.Command == 0 && trace.PayloadHex == "[图像内容省略]") == 3,
				      "protocol trace records real TX/RX frames and omits image content");
				int engineeringWrites = link.WriteCount;
				try { await session.SendEngineeringAsync(2, [0], default); throw new Exception("firmware write accepted"); }
				catch (ArgumentException) { }
				Check(link.WriteCount == engineeringWrites, "engineering firmware rejection happens before transmission");
				await session.SendEngineeringAsync(25, [93], default);
				Check((await session.QueryAsync(28, default)).SequenceEqual(new byte[] { 93 }), "engineering density writes require successful density readback");
				link.Media = 0x10;
				int before = link.Bitmaps.Count;
				try
				{
					await session.PrintAsync(new Raster(1, new byte[72]), Settings, default);
					throw new Exception("printed with cover open");
				}
				catch (InvalidOperationException)
				{
				}
				Check(link.Bitmaps.Count == before, "cover-open blocks all image writes");
				link.Media = 1;
				try
				{
					await session.PrintAsync(new Raster(1, new byte[72]), Settings, default);
					throw new Exception("printed without paper");
				}
				catch (InvalidOperationException)
				{
				}
				Check(link.Bitmaps.Count == before, "paper-out blocks image writes");
				link.Media = 8;
				link.WrongDensity = true;
				try
				{
					await session.PrintAsync(new Raster(1, new byte[72]), Settings, default);
					throw new Exception("printed at wrong density");
				}
				catch (InvalidOperationException)
				{
				}
				Check(link.Bitmaps.Count == before, "density read-back mismatch blocks image writes");
			}
			using (var link = new FakeBle()) using (var session = new P2Session(link))
			{
				link.Prime();
				await session.HandshakeAsync(default);
				var custom = new WorkspaceSettings { PacketBytes = 72, BleChunk = 40, WritePauseMS = 5, FinishWaitSeconds = 1 };
				await session.PrintAsync(new Raster(3, new byte[216]), Settings with { TransferSettings = custom, Feed = 0 }, default);
				Check(link.Bitmaps.Count == 3 && link.Bitmaps.All(frame => frame.Payload.Length == 72) &&
				      link.Bitmaps.Select(frame => frame.Index).SequenceEqual(new byte[] { 0, 1, 2 }),
				      "custom transfer preset controls actual BLE payload sizes and frame indices");
				int writes = link.WriteCount;
				try
				{
					await session.PrintAsync(new Raster(257, new byte[257 * 72]), Settings with { TransferSettings = custom }, default);
					throw new Exception("Frame index overflow accepted");
				}
				catch (InvalidDataException) { }
				Check(link.WriteCount == writes, "oversized custom partition rejected before any printer writes");
			}
			using (var link = new FakeBle { Model = "Paperang_P1" }) using (var session = new P2Session(link))
			{
				link.Prime();
				try
				{
					await session.HandshakeAsync(default);
					throw new Exception("accepted P1");
				}
				catch (InvalidOperationException)
				{
				}
				Check(!session.Ready, "non-P2 model rejected");
			}
			using (var link = new FakeBle()) using (var session = new P2Session(link))
			{
				using var cts = new CancellationTokenSource(80);
				try
				{
					await session.HandshakeAsync(cts.Token);
					throw new Exception("sent without credits");
				}
				catch (OperationCanceledException)
				{
				}
				Check(link.WriteCount == 0, "no BLE writes without FF03 credits; cancellation remains responsive");
			}
			Raster image = new(1, new byte[72]);
			var grayRow = Enumerable.Repeat(128.0, 576).ToArray();
			var ordered = (new WorkspaceSettings { Conversion = "有序抖动" }).ConvertGray(grayRow, 1);
			Check(ordered.Bytes.All(value => value == 0x55), "ordered dithering matches the Mac Bayer row golden vector");
			Check((new WorkspaceSettings { Invert = true }).ConvertGray(Enumerable.Repeat(255.0, 576).ToArray(), 1).Bytes.All(value => value == 255),
			      "tone inversion applies before one-bit packing");
			var patchedSettings = JsonSerializer.Deserialize<WorkspaceSettings>("{\"version\":1,\"marginX\":0,\"marginY\":0,\"contentWidth\":576,\"gamma\":1.3,\"feedAfter\":160}", RelayClient.Json)!.Validate();
			Check(JsonSerializer.Deserialize<WorkspaceSettings>(JsonSerializer.Serialize(patchedSettings, RelayClient.Json), RelayClient.Json) == patchedSettings,
			      "Mac schema 1 parameter names preserve tone and print options through JSON");
			foreach (var invalid in new[] { new WorkspaceSettings { Gamma = 0 }, new WorkspaceSettings { MarginX = 12 },
				new WorkspaceSettings { Contrast = double.NaN }, new WorkspaceSettings { Rotation = 45 } })
			{
				try { invalid.Validate(); throw new Exception("invalid workspace settings accepted"); }
				catch (InvalidDataException) { }
			}
			Check(true, "invalid tone, overflowing margins and non-quadrant rotation are rejected");
			var blackRow = new Raster(1, Enumerable.Repeat((byte)255, 72).ToArray());
			var layout = PaperLayout.Render(3, [new(blackRow, 7, 1, 3, 1), new(image, 8, 1, 1, 1)]);
			Check(layout.Bytes[72] == 1 && layout.Bytes[73] == 64 && layout.Bytes.Count(value => value != 0) == 2,
			      "free layout positions across byte boundaries and applies top-layer white occlusion");
			try
			{
				_ = PaperLayout.Render(1, [new(image, 575, 0, 2, 1)]);
				throw new Exception("accepted off-paper layer");
			}
			catch (InvalidDataException) { }
			Check(RelayTemplate.Defaults().SelectMany(template => template.Blocks).All(block => block.Margin == 0),
			      "new Windows relay templates use full paper width");
			var tight = NoteRenderer.RenderText("Ågj", "Arial", 30, false, "居左", 0, 0, 10);
			var inset = NoteRenderer.RenderText("Ågj", "Arial", 30, false, "居左", 0, 6, 0);
			Check(inset.Height == tight.Height + 12 && inset.Bytes.AsSpan(6 * 72, tight.Bytes.Length).SequenceEqual(tight.Bytes),
			      "explicit vertical margins add only requested rows; line spacing is not added after a single line");
			var padded = new Raster(3, new byte[216]);
			padded.Bytes[143] = 1;
			Check(padded.TrimTrailingBlankRows().Height == 2 && padded.TrimTrailingBlankRows().Bytes[143] == 1 &&
			          new Raster(3, new byte[216]).TrimTrailingBlankRows().Height == 1,
			      "trimming preserves leading blank rows and the last black dot");
			using (var printer = new FakePrinter())
			{
				await new LocalPrintOptions(73, 3, 9).SendAsync(printer, image, new(), default);
				Check(printer.LocalOperations.SequenceEqual(new[] { "feed:9", "print:73:0", "print:73:0", "print:73:160" }),
				      "local batches feed once before the first copy and 160 lines after the last by default");
				printer.LocalOperations.Clear();
				await new LocalPrintOptions(42, 2, After: 20, BetweenCopies: true).SendAsync(printer, image, new(), default);
				Check(printer.LocalOperations.SequenceEqual(new[] { "print:42:20", "print:42:20" }),
				      "local batches honor explicit feed after every copy");
			}
			var content = JsonSerializer.SerializeToElement(
			    new { type = "raster1", schemaVersion = 1, width = 576, height = 1, bitOrder = "msb-first",
				      blackIsOne = true, encoding = "base64", data = Convert.ToBase64String(image.Bytes),
				      sha256 = Convert.ToHexStringLower(SHA256.HashData(image.Bytes)) });
			Check((Job with { Content = content }).Image()!.Bytes.Length == 72, "raster1 checksum and size validation");
			var corrupt = JsonSerializer.SerializeToElement(
			    new { type = "raster1", schemaVersion = 1, width = 576, height = 1, bitOrder = "msb-first",
				      blackIsOne = true, encoding = "base64", data = Convert.ToBase64String(image.Bytes),
				      sha256 = new string('0', 64) });
			try
			{
				(Job with { Content = corrupt }).Image();
				throw new Exception("bad SHA accepted");
			}
			catch (InvalidDataException)
			{
				count++;
			}
			var raster = new NoteRenderer().Render(Job, Settings);
			Check(raster.Height is > 1 and <= 1400 && raster.Bytes.Any(x => x != 0),
			      "Windows GDI Chinese text produces packed black pixels");
			try
			{
				new NoteRenderer().Render(Job with { Text = new string('汉', 10000) }, Settings);
				throw new Exception("long note cropped");
			}
			catch (InvalidDataException)
			{
				count++;
			}
			TemplateCases();
			StorageAndVersionCases();
			await EngineCases();
			await UsbCases.Run(Check, Settings);
			// Real DPAPI storage is isolated to a temporary name via the pure encryption round-trip helper test below.
			var protect = typeof(LocalStore)
			                  .GetMethod("Protect", System.Reflection.BindingFlags.NonPublic |
			                                            System.Reflection.BindingFlags.Static)!;
			byte[] secret = Encoding.UTF8.GetBytes("psr_not_plaintext");
			byte[] encrypted = (byte[])protect.Invoke(null, [secret, true])!;
			Check(!Encoding.UTF8.GetString(encrypted).Contains("psr_") &&
			          ((byte[])protect.Invoke(null, [encrypted, false])!).SequenceEqual(secret),
			      "Windows user DPAPI encryption round-trip");
			Console.WriteLine($"ALL PASSED: {count}");
		}
		catch (Exception e)
		{
			Console.Error.WriteLine(e);
			Environment.ExitCode = 1;
		}
	}
	private static void StorageAndVersionCases()
	{
		Check(ReleaseUpdates.DistributionTag("v0.3.2", ["PaperangStudio-0.3.2-winlite-win-x64-setup.exe"]) ==
		          "0.3.2-winlite",
		      "product release maps to own distribution only with installer asset");
		Check(ReleaseUpdates.DistributionTag("v0.3.2", []) == null &&
		          ReleaseUpdates.DistributionTag("v0.3.2", ["PaperangStudio-0.3.2-macclient-macos-arm64.zip"]) == null,
		      "missing and foreign release packages are not offered");
		foreach (string old in new[] { "0.3.2-winlite", "0.3.1-winlite", "0.3.1-winlite-fix99", "0.3.1-winlite-opt99" })
			Check(ReleaseUpdates.Compare(AppVersion.Current, old) > 0, "WinLite update ordering over " + old);
		foreach (string invalid in new[] { "0.3.2", "0.3.2-macclient", "0.3.2-linuxrelayserver", "0.3.2-winlite-opt0",
		                                   "0.3.2-winlite-opt9999999999999" })
		{
			try
			{
				ReleaseUpdates.Compare(AppVersion.Current, invalid);
				throw new Exception("accepted invalid distribution");
			}
			catch (FormatException)
			{
				count++;
			}
		}
		Check(ReleaseUpdates.Compare("v" + AppVersion.Current, AppVersion.Current) == 0 &&
		          ReleaseUpdates.Compare("0.3.1-winlite-opt1", "0.3.1-winlite-fix99") > 0,
		      "distribution tags and historical opt/fix ordering");
		string directory = Path.Combine(Path.GetTempPath(), "ps032-store-" + Guid.NewGuid());
		try
		{
			var store = new LocalStore(directory);
			var saved = Settings with { OwnerKeyFile = "synthetic-owner.json", Style = "自定义",
			                            TemplateId = RelayTemplate.Defaults()[0].Id };
			store.SaveSettings(saved);
			store.SaveSettings(saved with { Density = 60 });
			var restored = new LocalStore(directory).LoadSettings();
			Check(restored.Density == 60 && restored.DeviceKey == saved.DeviceKey &&
			          restored.Templates.Count == saved.Templates.Count,
			      "DPAPI settings, credentials and templates survive new store instance");
			store.SaveDraft(saved with { Server = "", OwnerKeyFile = "" });
			Check(store.LoadForEditing().Server == saved.Server, "incomplete draft preserves saved connection");
			Check(File.Exists(Path.Combine(directory, "settings.dat.previous")),
			      "atomic replacement retains encrypted backup");
			var pending =
			    new PendingResult(Settings.Validate().AbsoluteUri, RelayEngine.Fingerprint(Settings.DeviceKey), Job.Id,
				                  "synthetic-claim", "sent");
			store.Save(pending);
			Check(new LocalStore(directory).Load() == pending, "durable sent journal round-trip");
			store.Save(null);
			Check(store.Load() == null, "successful report clears pending journal");
			Check(!ConfigurationExport.Serialize(saved, new()).Contains(saved.DeviceKey),
			      "configuration export excludes execution credential");
			File.WriteAllBytes(Path.Combine(directory, "settings.dat"), [1, 2, 3]);
			try
			{
				store.LoadSettings();
				throw new Exception("corrupt DPAPI silently accepted");
			}
			catch (System.ComponentModel.Win32Exception)
			{
				count++;
			}
		}
		finally
		{
			if (Directory.Exists(directory))
				Directory.Delete(directory, true);
		}
	}
	private static async Task ProbeProduction()
	{
		var saved = new LocalStore().LoadSettings();
		using var client = new HttpClient { BaseAddress = saved.Validate(), Timeout = TimeSpan.FromSeconds(30) };
		using var response = await client.GetAsync("v2/public/status");
		response.EnsureSuccessStatusCode();
		var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
		Console.WriteLine("Production public status HTTP " + (int)response.StatusCode);
		if (json.TryGetProperty("appVersion", out var version))
			Console.WriteLine("runtime=" + version.GetString());
		if (json.TryGetProperty("version", out version))
			Console.WriteLine("version=" + version.GetString());
		if (json.TryGetProperty("capabilities", out var capabilities))
			Console.WriteLine(capabilities);
	}
	private static async Task Physical(string directory)
	{
		Directory.CreateDirectory(directory);
		using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(90));
		var devices = await BlePrinter.ScanAsync(false, ct.Token);
		Console.WriteLine("P2 discovery candidates: " + devices.Length);
		var settings = new LocalStore().LoadSettings()
		                   with { Density = 60, Feed = 60, IncludeTime = false, IncludeSender = false };
		using var printer = new BlePrinter();
		await printer.ConnectAsync(settings, ct.Token);
		foreach (string label in new[] { "PS032-W-T1", "PS032-W-IT1" })
		{
			string attempt = Path.Combine(directory, label + ".json");
			// Exclusive intent record: rerunning refuses to replay either physical print.
			using (var file = new FileStream(attempt, FileMode.CreateNew, FileAccess.Write)) JsonSerializer.Serialize(
			    file, new { label, state = "attempting", version = AppVersion.Current, viaUbuntu = false });
			Raster raster;
			if (label.EndsWith("-T1"))
				raster = new NoteRenderer().Render(Job with { Text = label + "\nWindows 中文 / English 0.3.2" },
				                                   settings with { Style = "便签" });
			else
			{
				var sample = TemplateEditor.SampleJob(label + "\nImage + schema 1 template", true);
				var template = RelayTemplate.Preset("便签");
				raster = new NoteRenderer().Render(
				    sample, settings with { Style = "自定义", Templates = [template], TemplateId = template.Id });
			}
			await printer.PrintAsync(raster, settings, ct.Token);
			File.WriteAllText(attempt, JsonSerializer.Serialize(
			                               new { label, state = "sent", physicalCompletion = "unknown",
										         version = AppVersion.Current, viaUbuntu = false, raster.Height }));
			Console.WriteLine(label + " sent; paper confirmation required; rows=" + raster.Height);
			await Task.Delay(1500, ct.Token);
		}
		printer.Disconnect();
	}
	private static async Task Ble()
	{
		using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		var settings = new LocalStore().LoadSettings();
		if (settings.PrinterAddress.Length == 0)
		{
			var devices = await BlePrinter.ScanAsync(false, ct.Token);
			Console.WriteLine($"Scanned {devices.Length} candidates");
			if (devices.Length != 1)
				throw new InvalidOperationException("Select one P2 in settings before diagnostics");
			settings =
			    settings with { PrinterAddress = devices[0].Address.ToString("X12"), PrinterName = devices[0].Name };
		}
		using var printer =
		    new BlePrinter(message => Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + message));
		await printer.ConnectAsync(settings, ct.Token);
		Console.WriteLine("CONNECTED " + printer.Status);
		for (int i = 1; i <= 2; i++)
		{
			printer.Disconnect();
			await Task.Delay(1000, ct.Token);
			await printer.ConnectAsync(settings, ct.Token);
			Console.WriteLine($"RECONNECTED {i}/2 " + printer.Status);
		}
	}
	private static void TemplateCases()
	{
		var renderer = new NoteRenderer();
		var template = RelayTemplate.Preset("便签");
		var json = JsonSerializer.Serialize(template, RelayClient.Json);
		var roundtrip = JsonSerializer.Deserialize<RelayTemplate>(json, RelayClient.Json)!;
		roundtrip.Validate();
		using var document = JsonDocument.Parse(json);
		Check(document.RootElement.GetProperty("blocks") [0].GetProperty("font").GetString() == "PingFang SC" &&
		          roundtrip.Blocks.Count == 4,
		      "Swift Codable v1 template keys and fonts preserved");
		var job = TemplateEditor.SampleJob("{{ip}} — 中文说明", true);
		var block = new RelayBlock { Kind = "自定义文字", Text = "{{text}} / {{id}} / {{sender}}" };
		Check(block.Value(job).StartsWith("{{ip}} — 中文说明 / A1B2-C3D4-E5F6 / Sample visitor"),
		      "template variables expand once; visitor text never expanded recursively");
		template = template with { Blocks = [
			new RelayBlock { Kind = "标题", Text = "TEST", Gap = 5 },
			new RelayBlock { Kind = "正文", Margin = 0, Gap = 7 }, new RelayBlock { Kind = "投稿身份", Gap = 9 }
		] };
		var parts = renderer.Parts(job, template);
		var image = job.Image()!;
		Check(parts.Count == 3 && parts[1].Raster.Bytes.Take(image.Bytes.Length).SequenceEqual(image.Bytes),
		      "custom body places exact image before caption; margin zero preserves pixels");
		var settings = Settings with { Style = "自定义", Templates = [template], TemplateId = template.Id };
		var final = renderer.Render(job, settings);
		Check(final.Bytes.SequenceEqual(Raster.Stack(parts.Select(x => (x.Raster, x.Gap))).Bytes),
		      "editor block preview and real print share identical composition");
		int offset = (parts[0].Gap + parts[0].Raster.Height + parts[1].Gap) * 72;
		Check(final.Bytes.AsSpan(offset, image.Bytes.Length).SequenceEqual(image.Bytes),
		      "title/body/footer order and upper whitespace preserved");
		var noBody = template with {
			Blocks = template.Blocks.Select(x => x.Kind == "正文" ? x with { Enabled = false } : x).ToList()
		};
		Check(renderer.Parts(job, noBody).Count == 2, "disabling body removes both drawing and caption");
		var fit = image.Fitting(120);
		Check(fit.Height == (image.Height * 336 + 575) / 576 &&
		          fit.Bytes.Where((_, i) => i % 72 < 15 || i % 72 >= 57).All(x => x == 0),
		      "image fitting uses symmetric white margins and proportional height");
		foreach (var preset in RelayTemplate.Defaults())
		{
			preset.Validate();
			_ = Raster.Stack(renderer.Parts(job, preset).Select(x => (x.Raster, x.Gap)));
		}
		Check(true, "all four macOS presets render text and drawing");
		try
		{
			(template with { Blocks = [block with { Size = 97 }] }).Validate();
			throw new Exception("oversized font accepted");
		}
		catch (InvalidDataException)
		{
			count++;
		}
		try
		{
			Raster.Stack([(new Raster(1400, new byte[100800]), 1)]);
			throw new Exception("oversize composition accepted");
		}
		catch (InvalidDataException)
		{
			count++;
		}
	}
	private static async Task EngineCases()
	{
		var journal = new MemoryJournal();
		var handler = new RelayHandler(Job);
		var printer = new FakePrinter();
		var engine = new RelayEngine(printer, new FakeRenderer(), journal, s => new RelayClient(s, handler));
		await engine.StartAsync(Settings with { AutoAllowPrint = false });
		await Until(() => engine.Online);
		Check(handler.Claims == 0, "paused relay polls without claiming");
		engine.RequestPrint(Job.Id);
		await Until(() => journal.Value == null && handler.Sent == 1);
		Check(printer.Prints == 1 && handler.Claims == 1 && handler.HeadersValid,
		      "selected manual Relay task uses authenticated durable claim/print/result without enabling automatic printing");
		Check(!engine.Allowed, "manual Relay printing preserves automatic printing preference");
		engine.RequestPrint(Job.Id);
		await Until(() => engine.PrintMessage.Contains("已不在待处理队列"));
		Check(handler.Claims == 1 && printer.Prints == 1, "manual request never reclaims a task already sent");
		await engine.StopAsync();

		journal = new MemoryJournal { Value = new(Settings.Validate().AbsoluteUri,
		                                          RelayEngine.Fingerprint(Settings.DeviceKey), Job.Id, "oldclaim") };
		handler = new RelayHandler(Job) { State = "printing" };
		printer = new FakePrinter();
		engine = new(printer, new FakeRenderer(), journal, s => new RelayClient(s, handler));
		await engine.StartAsync(Settings);
		await Until(() => handler.Unknown == 1);
		Check(printer.Prints == 0 && journal.Value == null, "restart reports old claim unknown without reprinting");
		await engine.StopAsync();

		journal = new MemoryJournal();
		handler = new RelayHandler(Job) { FailResults = 2 };
		printer = new FakePrinter();
		engine = new(printer, new FakeRenderer(), journal, s => new RelayClient(s, handler));
		await engine.StartAsync(Settings);
		await Until(() => handler.Sent == 1);
		Check(printer.Prints == 1 && handler.Claims == 1, "lost final reports retried without second print");
		await engine.StopAsync();

		journal = new MemoryJournal();
		handler = new RelayHandler(Job);
		printer = new FakePrinter { Wait = true };
		engine = new(printer, new FakeRenderer(), journal, s => new RelayClient(s, handler));
		await engine.StartAsync(Settings);
		await Until(() => engine.Busy);
		Check(journal.Value?.State == "unknown", "claim is durable before physical send");
		engine.SetAllowed(false);
		await Until(() => handler.PrintingReports >= 2);
		Check(engine.Busy && handler.PrintingReports >= 2,
		      "pause preserves active send; printing claim heartbeats continue");
		engine.CancelForPaperPermission();
		await Until(() => !engine.Running);
		Check(!engine.Busy && journal.Value?.State == "unknown", "global paper cancellation stops an active Relay send and preserves uncertainty");
		await engine.StopAsync();
		Check(journal.Value?.State == "unknown" && !engine.Allowed, "shutdown mid-send leaves unknown and pauses");

		journal = new MemoryJournal { Value = new(Settings.Validate().AbsoluteUri,
		                                          RelayEngine.Fingerprint(Settings.DeviceKey), Job.Id, "x") };
		engine = new(new FakePrinter(), new FakeRenderer(), journal);
		try
		{
			await engine.StartAsync(Settings with { DeviceKey = "psr_other" });
			throw new Exception("changed credential with pending claim");
		}
		catch (InvalidOperationException)
		{
			count++;
		}
	}
	private static async Task Integration(string path)
	{
		var config = JsonDocument.Parse(await File.ReadAllTextAsync(path)).RootElement;
		var settings = Settings with { Server = config.GetProperty("server").GetString()!, AuthenticationMode = "owner",
		                               OwnerKeyFile = config.GetProperty("ownerPath").GetString()! };
		int expected = config.GetProperty("jobCount").GetInt32();
		bool physical = config.GetProperty("physical").GetBoolean();
		using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(45));
		foreach (string file in new[] { "ownerPath", "pkcs1Path" })
		{
			using var client = new RelayClient(settings with { OwnerKeyFile = config.GetProperty(file).GetString()! });
			var me = await client.Get("v2/auth/me", ct.Token);
			Check(me.GetProperty("role").GetString() == "C", "native RSA proof " + file);
			var owner = typeof(RelayClient)
			                .GetField("owner", System.Reflection.BindingFlags.NonPublic |
			                                       System.Reflection.BindingFlags.Instance)!.GetValue(client)!;
			typeof(OwnerAuthentication)
			    .GetField("expires", System.Reflection.BindingFlags.NonPublic |
				                         System.Reflection.BindingFlags.Instance)!.SetValue(owner,
				                                                                            DateTimeOffset.MinValue);
			Check((await client.Get("v2/auth/me", ct.Token)).GetProperty("role").GetString() == "C",
			      "expired native owner session renews via proof");
			var history = await client.Post("v2/admin/jobs/search", new {}, ct.Token);
			Check(history.GetProperty("jobs").GetArrayLength() == expected, "C historical payload access");
			Check((await client.Get("v2/device/jobs", ct.Token)).GetProperty("jobs").GetArrayLength() == expected,
			      "proven owner device execution");
			var quota = await client.Post("v2/admin/quotas/get", new { scope = "authenticated" }, ct.Token);
			Check(quota.ValueKind == JsonValueKind.Object, "C quota management read");
		}
		var mSettings =
		    Settings with { Server = settings.Server, DeviceKey = config.GetProperty("mToken").GetString()! };
		using (var client = new RelayClient(mSettings))
		{
			try
			{
				await client.Post("v2/admin/jobs/search", new {}, ct.Token);
				throw new Exception("M read history");
			}
			catch (RelayHttpException e) when (e.Status == 403)
			{
				Check(true, "M cannot administer history");
			}
		}
		using IPrinter printer = physical ? new BlePrinter() : new FakePrinter();
		if (physical)
		{
			var saved = new LocalStore().LoadSettings();
			settings = settings with { PrinterAddress = saved.PrinterAddress,
			                           PrinterName = saved.PrinterName,
			                           Density = 60,
			                           Feed = 60,
			                           IncludeSender = false,
			                           IncludeTime = false,
			                           Style = "便签" };
		}
		var journal = new MemoryJournal();
		var engine = new RelayEngine(printer, new NoteRenderer(), journal,
		                             timing: new(TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(50)));
		bool wasPending = false;
		engine.Changed += () =>
		{
			if (journal.Value != null)
				wasPending = true;
		};
		await engine.StartAsync(settings);
		await Until(() => wasPending && journal.Value == null && !engine.Busy && engine.Queued == 0, 30000);
		await engine.StopAsync();
		Check(physical || ((FakePrinter)printer).Prints == expected,
		      physical ? "isolated Relay actual P2 send; paper confirmation required"
		               : "isolated owner Relay text and raster1; fake printer only");
		Console.WriteLine("ALL INTEGRATION PASSED: " + count);
	}
	sealed class MemoryJournal : IJournal
	{
		public PendingResult? Value;
		public PendingResult? Load() => Value;
		public void Save(PendingResult? v) => Value = v;
	}
	sealed class FakeRenderer : IRasterizer
	{
		public Raster Render(RelayJob j, AppSettings s) => new(1, new byte[72]);
	}
	sealed class FakePrinter : IPrinter
	{
		public bool Ready { get; private set; }
		public string Status => "Fake P2";
		public double Progress => 0;
		public int Prints;
		public bool Wait;
		public List<string> LocalOperations { get; } = [];
		public Task ConnectAsync(AppSettings s, CancellationToken ct)
		{
			Ready = true;
			return Task.CompletedTask;
		}
		public async Task PrintAsync(Raster r, AppSettings s, CancellationToken ct)
		{
			Prints++;
			LocalOperations.Add($"print:{s.Density}:{s.Feed}");
			if (Wait)
				await Task.Delay(Timeout.Infinite, ct);
		}
		public Task RefreshAsync(CancellationToken ct) => Task.CompletedTask;
		public Task ApplyDensityAsync(int density, CancellationToken ct) => Task.CompletedTask;
		public Task FeedAsync(int lines, CancellationToken ct)
		{
			LocalOperations.Add($"feed:{lines}");
			return Task.CompletedTask;
		}
		public void Disconnect() => Ready = false;
		public void Dispose()
		{
		}
	}
	sealed class RelayHandler(RelayJob job) : HttpMessageHandler
	{
		public int Claims, Sent, Unknown, FailResults, PrintingReports;
		public bool HeadersValid = true;
		public string State = "queued";
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
		{
			HeadersValid &= req.Headers.Authorization?.Parameter == "psr_test" &&
			                req.Headers.GetValues("X-Paperang-Content-Types").Single() == "text,raster1";
			if (req.Content != null)
				HeadersValid &= req.Content.Headers.ContentLength > 0;
			string p = req.RequestUri!.AbsolutePath;
			if (!p.StartsWith("/v2/", StringComparison.Ordinal))
				throw new Exception("non-v2 runtime request");
			object result = new { accepted = true };
			if (p.EndsWith("/claim"))
			{
				Claims++;
				State = "claimed";
				result = new { claim = "claim" };
			}
			else if (p.EndsWith("/result"))
			{
				var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct)).RootElement;
				string state = body.GetProperty("state").GetString()!;
				if (state == "sent" && FailResults-- > 0)
					return new(HttpStatusCode.ServiceUnavailable);
				State = state;
				if (state == "sent")
					Sent++;
				if (state == "unknown")
					Unknown++;
				if (state == "printing")
					PrintingReports++;
			}
			else if (p == "/v2/device/jobs")
				result = new { jobs = new[] { job with { State = State } } };
			else if (p == "/v2/device/jobs/" + job.Id)
			{
				if (req.Headers.GetValues("X-Job-Claim").Single() != "claim")
					throw new Exception("missing claim authority");
				result = job with { State = State };
			}
			return new(HttpStatusCode.OK) { Content =
			                                    new StringContent(JsonSerializer.Serialize(result, RelayClient.Json)) };
		}
		protected override void Dispose(bool disposing)
		{
		} // factory reuses the fixture
	}
	sealed class FakeBle : IBleLink
	{
		public event Action<byte[]>? Data;
		public event Action<byte[]>? Flow;
		public bool Connected => true;
		public int MaxWrite => 20;
		public string Model = "Paperang_P2";
		public ushort Media = 8;
		public bool WrongDensity;
		public int WriteCount;
		public List<Frame> Bitmaps = [];
		public byte[] Feed = [];
		private byte density = 100;
		private readonly List<byte> buffer = [];
		public void Prime() => Flow?.Invoke([1, 1]);
		public Task WriteAsync(byte[] bytes, CancellationToken ct)
		{
			WriteCount++;
			buffer.AddRange(bytes);
			Flow?.Invoke([1, 1]);
			if (buffer.Count < 10)
				return Task.CompletedTask;
			int n = buffer[3] | buffer[4] << 8;
			if (buffer.Count < n + 10)
				return Task.CompletedTask;
			byte command = buffer[1], index = buffer[2];
			byte[] payload = buffer.GetRange(5, n).ToArray();
			uint crc = BitConverter.ToUInt32(buffer.GetRange(5 + n, 4).ToArray());
			if (crc != Wire.Crc(payload, command == 24 ? 0x35769521u : 0))
				throw new Exception("CRC mismatch");
			buffer.RemoveRange(0, n + 10);
			if (command == 0)
			{
				Bitmaps.Add(new(command, index, payload));
				return Task.CompletedTask;
			}
			if (command == 25)
				density = payload[0];
			if (command == 26)
				Feed = payload;
			byte[]? reply = command switch {
				8 or 10 => [1, 2, 3],
				6 => Encoding.UTF8.GetBytes(Model),
				16 => [80],
				12 => [(byte)Media, (byte)(Media >> 8)],
				28 => [WrongDensity ? (byte)0 : density],
				_ => null
			};
			byte[] frames = Wire.Encode(command, [0]);
			if (reply != null)
				frames = frames.Concat(Wire.Encode(P2Queries.Expected(command), reply)).ToArray();
			Data?.Invoke(frames[..7]);
			Data?.Invoke(frames[7..]);
			return Task.CompletedTask;
		}
		public void Dispose()
		{
		}
	}
}
