using System.Net;
using System.Text;
using System.Text.Json;
using Paperang.Core;
using Paperang.Windows;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private async Task CheckAddedFeatures(string directory)
	{
		string path = Path.Combine(directory, "source.png");
		using (var source = new System.Drawing.Bitmap(4, 2))
		{
			source.SetPixel(0, 0, System.Drawing.Color.Black);
			source.SetPixel(3, 1, System.Drawing.Color.Black);
			source.Save(path, System.Drawing.Imaging.ImageFormat.Png);
		}
		var full = WorkspaceFiles.Open(path, new WorkspaceSettings());
		var pixels = WorkspaceFiles.Open(path, new WorkspaceSettings { ImageFit = "保持像素" });
		var rotated = WorkspaceFiles.Open(path, new WorkspaceSettings { Rotation = 90 });
		var cropped = WorkspaceFiles.Open(path, new WorkspaceSettings { ImageFit = "裁切填满", CropHeight = 60 });
		if (full.Height != 288 || pixels.Height != 2 || rotated.Height != 1152 || cropped.Height != 60)
			throw new InvalidOperationException("Image fit, native size, rotation or crop geometry is incorrect");
		if (!pixels.Bytes.Any(value => value != 0) || pixels.Bytes.Count(value => value != 0) > 2)
			throw new InvalidOperationException("Transparent source pixels were not composited onto white");
		var preset = WorkspaceSettings.Preset("照片") with { Gamma = 1.4, MarginX = 12, ContentWidth = 552,
			PacketBytes = 720, BleChunk = 80, WritePauseMS = 25, FramePauseMS = 12, TimeoutSeconds = 9, FinishWaitSeconds = 3 };
		ApplyWorkspaceSettings(preset);
		if (ReadWorkspaceSettings() != preset)
			throw new InvalidOperationException("Preset controls did not restore the complete parameter set");
		ApplyWorkspaceSettings(new());
		templateGap.Value = 0;
		blockSize.Value = 23;
		ApplyTemplateBlock();
		var current = CurrentTemplate();
		if (current.Blocks[0].Gap != 0 || current.Blocks[0].Size != 23)
			throw new InvalidOperationException("Template block changes were not applied");
		var json = JsonSerializer.Serialize(current, RelayClient.Json);
		var restored = JsonSerializer.Deserialize<RelayTemplate>(json, RelayClient.Json)!;
		restored.Validate();
		var sample = new RelayJob { Id = Guid.NewGuid().ToString(), Text = "模板内容\n保留区块顺序" };
		var first = new NoteRenderer().Render(sample, new AppSettings { Style = "自定义", Templates = [current], TemplateId = current.Id });
		var second = new NoteRenderer().Render(sample, new AppSettings { Style = "自定义", Templates = [restored], TemplateId = restored.Id });
		if (!first.Bytes.AsSpan().SequenceEqual(second.Bytes))
			throw new InvalidOperationException("Template JSON round-trip changed the print raster");
		if (Environment.GetEnvironmentVariable("PAPERANG_WORKSPACE_QR_DIR") is string qrDirectory)
		{
			Directory.CreateDirectory(qrDirectory);
			int index = 0;
			foreach (string format in new[] { "纯文字", "网页链接", "电子邮件" })
				foreach (string shape in new[] { "方形", "圆角", "圆点" })
					foreach (bool border in new[] { false, true })
					{
						string value = format switch { "网页链接" => "https://example.com/path", "电子邮件" => "print@example.com", _ => "Paperang 中文 test" };
						var raster = WorkspaceFiles.Qr(value, 360, "H", format, shape, border);
						string stem = Path.Combine(qrDirectory, (++index).ToString("D2"));
						WorkspaceFiles.Export(stem + ".png", raster);
						await File.WriteAllTextAsync(stem + ".txt", format == "电子邮件" ? "mailto:" + value : value);
					}
		}
		await CheckAdministration();
		CheckLayerEditing();
		using (var gray = new System.Drawing.Bitmap(4, 2))
		{
			using var graphics = System.Drawing.Graphics.FromImage(gray);
			graphics.Clear(System.Drawing.Color.FromArgb(128, 128, 128));
			gray.Save(path, System.Drawing.Imaging.ImageFormat.Png);
		}
		var graySource = new LayerSource("image", Settings: new WorkspaceSettings { Threshold = 80 },
			ImageBase64: Convert.ToBase64String(WorkspaceImage.CaptureGray(path, new())));
		var imageLayer = new PaperLayer(new Raster(100, new byte[7200]), 0, 0, 280, 100,
			Source: JsonSerializer.SerializeToElement(graySource, RelayClient.Json));
		var dark = RenderEditableLayer(imageLayer);
		var light = RenderEditableLayer(imageLayer with { Source = JsonSerializer.SerializeToElement(graySource with {
			Settings = graySource.Settings! with { Threshold = 180 } }, RelayClient.Json) });
		if (dark.Image.Bytes.Any(value => value != 0) || !light.Image.Bytes.Any(value => value != 0))
			throw new InvalidOperationException("Image objects discarded their original gray levels");
		var roundTrip = JsonSerializer.Deserialize<PaperLayer>(JsonSerializer.Serialize(imageLayer, RelayClient.Json), RelayClient.Json)!;
		if (!RenderEditableLayer(roundTrip).Image.Bytes.AsSpan().SequenceEqual(dark.Image.Bytes))
			throw new InvalidOperationException("Editable image gray source did not survive restoration");
		Console.WriteLine("PASS editable image objects retain gray levels, re-render tone changes and restore their sources");
		Console.WriteLine("PASS image transforms, transparency, parameter presets, template editing and management pagination");
	}

	private async Task CheckAdministration()
	{
		using (var bootstrap = new AdminLoginFixture())
		{
			var settings = new AppSettings { Server = "http://localhost", AuthenticationMode = "legacy", DeviceKey = "psr_login" };
			try { await LoginAdminCredential(settings, "fixture credential", default, bootstrap); throw new Exception("Non-C session accepted"); }
			catch (InvalidOperationException) { }
			if (adminClient != null) throw new Exception("Rejected management session retained credentials");
			bootstrap.Role = "C";
			await LoginAdminCredential(settings, "fixture credential", default, bootstrap);
			if (adminSessionServer != "http://localhost" || adminExpires > DateTimeOffset.UtcNow.AddSeconds(600))
				throw new Exception("Management bootstrap server or bounded expiry is incorrect");
			adminExpires = DateTimeOffset.UtcNow.AddSeconds(-1);
			ExpireAdministration();
			if (adminClient != null || adminSessionServer != null) throw new Exception("Expired management session remained unlocked");
		}
		using var fixture = new AdminFixture();
		adminClient = new RelayClient(new AppSettings { Server = "http://localhost", AuthenticationMode = "legacy", DeviceKey = "psr_test" }, fixture);
		try
		{
			for (int category = 0; category < 5; category++)
			{
				adminCategory.SelectedIndex = category;
				await LoadAdminPage(false, default);
				if (adminRows.Items.Count != 1 || adminNext != 1) throw new InvalidOperationException("Management first page failed");
				if (category == 2)
				{
					adminRows.SelectedIndex = 0;
					if (SelectedAdminId() != "fixture") throw new Exception("Session credential ID was not recognized");
				}
				await LoadAdminPage(true, default);
				if (adminPrevious.Count != 1 || adminBefore != 1 || adminNext != null)
					throw new InvalidOperationException("Management next page failed");
			}
			adminCategory.SelectedIndex = 0;
			await LoadAdminPage(false, default);
			fixture.RepeatCursor = true;
			try { await LoadAdminPage(true, default); throw new Exception("Non-progressing cursor was accepted"); }
			catch (InvalidDataException) { }
			if (adminBefore != null || adminNext != 1 || adminPrevious.Count != 0)
				throw new InvalidOperationException("Failed pagination changed the current page");
		}
		finally
		{
			LockAdministration();
			adminCategory.SelectedIndex = 0;
		}
	}

	private sealed class AdminFixture : HttpMessageHandler
	{
		internal bool RepeatCursor;
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
		{
			if (request.Method != HttpMethod.Post) throw new Exception("Management search must use POST");
			using var input = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
			double before = input.RootElement.GetProperty("before").GetDouble();
			string property = request.RequestUri!.AbsolutePath switch
			{
				"/v2/admin/keys/search" => "keys", "/v2/admin/identities/search" => "identities",
				"/v2/admin/sessions/search" => "sessions", "/v2/admin/jobs/search" => "jobs",
				"/v2/admin/audit" => "events", _ => throw new Exception("Unexpected management endpoint")
			};
			object row = property == "sessions" ? new { credential_id = "fixture", browser_id = "browser", expires = 1795000000 } :
				new { id = "fixture", name = "fixture" };
			object response = new Dictionary<string, object?>
			{
				[property] = new[] { row },
				["next"] = RepeatCursor ? before : before > 1 ? 1 : null
			};
			return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json") };
		}
	}
	private sealed class AdminLoginFixture : HttpMessageHandler
	{
		internal string Role = "M";
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
		{
			if (request.RequestUri!.AbsolutePath != "/v2/auth/login") throw new Exception("Unexpected login route");
			using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
			if (body.RootElement.GetProperty("credential").GetString() != "fixture credential") throw new Exception("Login credential changed");
			return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { role = Role, token = "pss_fixture", maxAge = 86400 })) };
		}
		protected override void Dispose(bool disposing) { }
	}
}
