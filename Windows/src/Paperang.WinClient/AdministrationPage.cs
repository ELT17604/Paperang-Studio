using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Paperang.Core;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private readonly StackPanel administrationPage = new() { Spacing = 12 };
	private readonly ComboBox adminCategory = Choice("管理类别", "凭证", "身份", "浏览器会话", "任务历史", "审计记录");
	private readonly TextBox adminUser = new() { Header = "稳定用户 ID 筛选", MaxLength = 128 };
	private readonly CheckBox adminHideRevoked = new() { Content = "隐藏已吊销", IsChecked = true };
	private readonly ComboBox adminLimit = Choice("每页", "25", "50", "100");
	private readonly ListView adminRows = new() { MaxHeight = 360 };
	private readonly TextBox adminDetail = new() { Header = "详情", IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 300 };
	private readonly TextBox adminName = new() { Header = "展示名称", MaxLength = 80 };
	private readonly ComboBox adminRole = Choice("权限", "A", "B", "C", "M");
	private readonly ComboBox adminKind = Choice("凭证种类", "token", "passphrase");
	private readonly PasswordBox adminPassword = new() { Header = "自定义口令（可选）" };
	private readonly TextBox adminIdentity = new() { Header = "关联已有身份 ID（空白新建）", MaxLength = 128 };
	private readonly TextBlock adminStatus = Paragraph("已锁定，请使用已配对的所有者私钥登录。");
	private RelayClient? adminClient;
	private readonly PasswordBox adminCredential = new() { Header = "C 密钥或完整口令", MaxLength = 512 };
	private DateTimeOffset adminExpires = DateTimeOffset.MaxValue;
	private readonly TextBox adminServer = new() { Header = "管理服务器根地址", PlaceholderText = "https://relay.example.com" };
	private readonly CheckBox adminHttp = new() { Content = "允许管理服务器使用公网 HTTP" };
	private string? adminSessionServer;
	private double? adminNext;
	private double? adminBefore;
	private readonly List<double?> adminPrevious = [];
	private sealed record AdminRow(JsonElement Value)
	{
		public override string ToString() => string.Join(" · ", new[] { "id", "credential_id", "browser_id", "name", "role", "kind", "state", "created", "expires", "action", "target" }
			.Where(name => Value.TryGetProperty(name, out _)).Select(name => Value.GetProperty(name).ToString()));
	}

	private void BuildAdministration()
	{
		administrationPage.Children.Add(Heading("访问与任务管理"));
		administrationPage.Children.Add(adminStatus);
		try
		{
			adminServer.Text = trayHost?.Settings().Server ?? "";
			adminHttp.IsChecked = trayHost?.Settings().AllowHttp ?? false;
		}
		catch (Exception error) { Report(error.Message, InfoBarSeverity.Error); }
		adminServer.TextChanged += (_, _) => LockAdministration();
		adminHttp.Click += (_, _) => LockAdministration();
		administrationPage.Children.Add(adminServer);
		administrationPage.Children.Add(adminHttp);
		administrationPage.Children.Add(adminCredential);
		administrationPage.Children.Add(ActionButton("使用 C 密钥或口令登录", async token =>
		{
			string credential = adminCredential.Password;
			if (credential.Length == 0) throw new InvalidDataException("请输入完整 C 凭证。");
			LockAdministration();
			var settings = tray.Settings() with { Server = adminServer.Text.Trim(), AllowHttp = adminHttp.IsChecked == true,
				AuthenticationMode = "legacy", DeviceKey = "psr_login" };
			await LoginAdminCredential(settings, credential, token);
			await LoadAdminPage(false, token);
		}));
		administrationPage.Children.Add(ActionButton("所有者私钥登录", async token =>
		{
			LockAdministration();
			var settings = tray.Settings() with { Server = adminServer.Text.Trim(), AllowHttp = adminHttp.IsChecked == true, AuthenticationMode = "owner" };
			string serverRoot = settings.Validate().AbsoluteUri.TrimEnd('/');
			var client = new RelayClient(settings, productVersion: Program.Version);
			try
			{
				var identity = await client.Get("v2/auth/me", token);
				if (identity.GetProperty("role").GetString() != "C")
					throw new InvalidOperationException("管理中心需要 C 权限。");
				adminClient = client;
				adminSessionServer = serverRoot;
				adminExpires = DateTimeOffset.UtcNow.AddSeconds(540);
				adminStatus.Text = "所有者会话已验证";
			}
			catch { client.Dispose(); throw; }
			await LoadAdminPage(false, token);
		}));
		administrationPage.Children.Add(SimpleAction("锁定", LockAdministration));
		administrationPage.Children.Add(TwoColumns(adminCategory, adminLimit));
		administrationPage.Children.Add(adminUser);
		administrationPage.Children.Add(adminHideRevoked);
		administrationPage.Children.Add(ActionButton("刷新列表", token => LoadAdminPage(false, token)));
		var paging = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		paging.Children.Add(ActionButton("上一页", async token =>
		{
			if (adminPrevious.Count == 0) return;
			adminBefore = adminPrevious[^1];
			adminPrevious.RemoveAt(adminPrevious.Count - 1);
			await FetchAdminPage(token);
		}));
		paging.Children.Add(ActionButton("下一页", token => LoadAdminPage(true, token)));
		administrationPage.Children.Add(paging);
		adminRows.SelectionChanged += (_, _) => adminDetail.Text = adminRows.SelectedItem is AdminRow row
			? JsonSerializer.Serialize(row.Value, RelayClient.Json) : "";
		adminCategory.SelectionChanged += (_, _) => ResetAdminPage();
		adminUser.TextChanged += (_, _) => ResetAdminPage();
		adminHideRevoked.Click += (_, _) => ResetAdminPage();
		adminLimit.SelectionChanged += (_, _) => ResetAdminPage();
		administrationPage.Children.Add(adminRows);
		administrationPage.Children.Add(adminDetail);
		administrationPage.Children.Add(SimpleAction("清除详情", () => adminDetail.Text = ""));
		administrationPage.Children.Add(ActionButton("读取完整任务详情", async token =>
		{
			if (adminCategory.SelectedIndex != 3) throw new InvalidOperationException("请在任务历史中选择任务。");
			var result = await AdminClient().Post("v2/admin/jobs/search", new { job_id = SelectedAdminId(), limit = 1 }, token);
			adminDetail.Text = JsonSerializer.Serialize(result, RelayClient.Json);
		}));
		administrationPage.Children.Add(adminName);
		administrationPage.Children.Add(TwoColumns(adminRole, adminKind));
		administrationPage.Children.Add(adminPassword);
		administrationPage.Children.Add(adminIdentity);
		administrationPage.Children.Add(ActionButton("创建凭证…", async token =>
		{
			string name = adminName.Text.Trim();
			if (name.Length == 0) throw new InvalidDataException("请输入展示名称。");
			string role = (string)adminRole.SelectedItem, kind = (string)adminKind.SelectedItem;
			if (role == "M" && kind != "token") throw new InvalidDataException("设备执行凭证必须使用随机密钥。");
			if (!await Confirm(administrationPage, "创建凭证？", $"{name} · 权限 {role} · {kind}")) return;
			var result = await AdminClient().Post("v2/admin/keys", new { name, role, kind,
				password = adminPassword.Password.Length == 0 ? null : adminPassword.Password,
				user_id = adminIdentity.Text.Trim().Length == 0 ? null : adminIdentity.Text.Trim() }, token);
			adminPassword.Password = "";
			await ShowGeneratedCredential(result);
			adminCategory.SelectedIndex = 0;
			await LoadAdminPage(false, token);
		}));
		administrationPage.Children.Add(ActionButton("改名…", async token =>
		{
			RequireAdminCategory(0);
			string name = adminName.Text.Trim();
			if (name.Length == 0) throw new InvalidDataException("请输入展示名称。");
			string id = SelectedAdminId();
			if (!await Confirm(administrationPage, "修改展示名称？", id + " → " + name)) return;
			await AdminClient().Post($"v2/admin/keys/{Uri.EscapeDataString(id)}/rename", new { name }, token);
			await FetchAdminPage(token);
		}));
		administrationPage.Children.Add(ActionButton("轮换凭证…", async token =>
		{
			RequireAdminCategory(0);
			var row = SelectedAdminRow();
			if (row.TryGetProperty("kind", out var kind) && kind.GetString() is "publickey" or "pendingkey")
				throw new InvalidOperationException("公钥凭证需先配对替代公钥，再吊销旧凭证。");
			string id = SelectedAdminId();
			if (!await Confirm(administrationPage, "轮换凭证？", "旧凭证将失效：" + id)) return;
			object body = adminPassword.Password.Length == 0 ? new { } : new { password = adminPassword.Password };
			var result = await AdminClient().Post($"v2/admin/keys/{Uri.EscapeDataString(id)}/rotate", body, token);
			adminPassword.Password = "";
			await ShowGeneratedCredential(result);
			await FetchAdminPage(token);
		}));
		administrationPage.Children.Add(ActionButton("吊销…", async token =>
		{
			var row = SelectedAdminRow();
			int category = adminCategory.SelectedIndex;
			if (category is < 0 or > 2) throw new InvalidOperationException("请选择凭证、身份或浏览器会话。");
			string id = SelectedAdminId();
			if (!await Confirm(administrationPage, "吊销所选对象？", "后续访问将被拒绝：" + id)) return;
			string path = category switch { 0 => $"v2/admin/keys/{Uri.EscapeDataString(id)}/revoke",
				1 => $"v2/admin/identities/{Uri.EscapeDataString(id)}/revoke", _ => "v2/admin/sessions/revoke" };
			object body = category == 2 ? new { credential_id = id } : new { };
			await AdminClient().Post(path, body, token);
			await FetchAdminPage(token);
		}));
		administrationPage.Children.Add(ActionButton("确认纸面完成…", async token =>
		{
			RequireAdminCategory(3);
			string id = SelectedAdminId();
			if (!await Confirm(administrationPage, "确认实际纸面已完成？", id)) return;
			await AdminClient().Post($"v2/admin/jobs/{Uri.EscapeDataString(id)}/confirm", new { }, token);
			await FetchAdminPage(token);
		}));
		administrationPage.Children.Add(ActionButton("导出历史 JSONL…", ExportAdminHistory));
		BuildAdministrationTools();
		Closed += (_, _) => LockAdministration();
	}

	private async Task LoginAdminCredential(AppSettings settings, string credential, CancellationToken token, HttpMessageHandler? handler = null)
	{
		LockAdministration();
		string server = settings.Validate().AbsoluteUri.TrimEnd('/');
		using var login = new RelayClient(settings, handler, productVersion: Program.Version);
		var result = await login.Post("v2/auth/login", new { credential }, token);
		if (result.GetProperty("role").GetString() != "C") throw new InvalidOperationException("管理中心需要 C 权限。");
		string access = result.GetProperty("token").GetString() ?? throw new InvalidDataException("服务器缺少会话凭据。");
		int age = result.TryGetProperty("maxAge", out var maximum) ? maximum.GetInt32() : 600;
		adminClient = new RelayClient(settings with { DeviceKey = access }, handler, productVersion: Program.Version);
		adminSessionServer = server;
		adminExpires = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(age, 1, 600));
		adminStatus.Text = "C 管理会话已验证；会话到期后自动锁定";
	}
	private RelayClient AdminClient()
	{
		ExpireAdministration();
		return adminClient ?? throw new InvalidOperationException("请先登录管理中心。");
	}
	private void ExpireAdministration()
	{
		if (adminClient != null && DateTimeOffset.UtcNow >= adminExpires) LockAdministration();
	}
	private JsonElement SelectedAdminRow() => adminRows.SelectedItem is AdminRow row ? row.Value
		: throw new InvalidOperationException("请先选择一个对象。");
	private string SelectedAdminId()
	{
		var row = SelectedAdminRow();
		if ((row.TryGetProperty("id", out var id) || row.TryGetProperty("credential_id", out id)) &&
			id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString())) return id.GetString()!;
		throw new InvalidDataException("对象没有有效 ID。");
	}
	private void RequireAdminCategory(int category)
	{
		if (adminCategory.SelectedIndex != category) throw new InvalidOperationException("当前管理类别不支持此操作。");
	}
	private void ResetAdminPage()
	{
		adminBefore = adminNext = null;
		adminPrevious.Clear();
		adminRows.ItemsSource = null;
		adminDetail.Text = "";
	}
	private void LockAdministration()
	{
		adminClient?.Dispose();
		adminClient = null;
		adminSessionServer = null;
		adminExpires = DateTimeOffset.MaxValue;
		adminCredential.Password = "";
		adminPassword.Password = "";
		relayDetail.Text = "";
		relayPaper.Source = null;
		adminStatus.Text = "管理中心已锁定";
		ResetAdminPage();
	}
	private async Task LoadAdminPage(bool next, CancellationToken token)
	{
		double? previousBefore = adminBefore, previousNext = adminNext;
		var previousPages = adminPrevious.ToArray();
		if (next)
		{
			if (adminNext == null) return;
			adminPrevious.Add(adminBefore);
			adminBefore = adminNext;
		}
		else ResetAdminPage();
		try { await FetchAdminPage(token); }
		catch
		{
			adminBefore = previousBefore; adminNext = previousNext;
			adminPrevious.Clear(); adminPrevious.AddRange(previousPages);
			throw;
		}
	}
	private async Task FetchAdminPage(CancellationToken token)
	{
		int category = adminCategory.SelectedIndex, limit = int.Parse((string)adminLimit.SelectedItem);
		string[] paths = ["keys/search", "identities/search", "sessions/search", "jobs/search", "audit"];
		string[] properties = ["keys", "identities", "sessions", "jobs", "events"];
		object query = category == 3 ? new { user_id = adminUser.Text.Trim(), before = adminBefore ?? 1e20, limit, summary = true } :
			category == 4 ? new { before = adminBefore.HasValue ? (long)adminBefore.Value : long.MaxValue, limit } :
			new { before = adminBefore ?? 1e20, limit, hide_revoked = adminHideRevoked.IsChecked == true };
		var result = await AdminClient().Post("v2/admin/" + paths[category], query, token);
		var next = result.TryGetProperty("next", out var cursor) && cursor.ValueKind == JsonValueKind.Number ? cursor.GetDouble() : (double?)null;
		if (next.HasValue && next >= (adminBefore ?? 1e20)) throw new InvalidDataException("服务器分页游标没有前进。");
		adminNext = next;
		adminRows.ItemsSource = result.GetProperty(properties[category]).EnumerateArray().Select(item => new AdminRow(item.Clone())).ToArray();
		adminStatus.Text = $"已登录 · {adminPrevious.Count + 1} 页" + (adminNext.HasValue ? " · 可继续翻页" : " · 已到末页");
	}
	private async Task ShowGeneratedCredential(JsonElement result)
	{
		var content = new TextBox { Text = JsonSerializer.Serialize(result, RelayClient.Json), IsReadOnly = true,
			AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinWidth = 420, MaxHeight = 420 };
		await new ContentDialog { Title = "凭证仅在此显示一次，请自行保存", Content = content,
			XamlRoot = administrationPage.XamlRoot, CloseButtonText = "关闭并清除" }.ShowAsync();
		content.Text = "";
	}
	private async Task ExportAdminHistory(CancellationToken token)
	{
		var client = AdminClient();
		var picker = new FileSavePicker { SuggestedFileName = "Paperang-history" };
		picker.FileTypeChoices.Add("历史 JSONL", new List<string> { ".jsonl" });
		InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
		var file = await picker.PickSaveFileAsync();
		if (file == null) return;
		await using var output = new StreamWriter(file.Path, false, new UTF8Encoding(false));
		double before = 1e20;
		while (true)
		{
			var page = await client.Post("v2/admin/jobs/export", new { user_id = adminUser.Text.Trim(), before }, token);
			var items = page.GetProperty("jobs").EnumerateArray().ToArray();
			foreach (var item in items) await output.WriteLineAsync(item.GetRawText().AsMemory(), token);
			if (items.Length == 0 || !page.TryGetProperty("next", out var next) || next.ValueKind != JsonValueKind.Number) break;
			double cursor = next.GetDouble();
			if (cursor >= before) throw new InvalidDataException("服务器导出游标没有前进。");
			before = cursor;
		}
		Report("历史已导出。", InfoBarSeverity.Success);
	}
}
