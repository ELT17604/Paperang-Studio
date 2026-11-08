using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Paperang.Core;

namespace Paperang.WinLite;

/// Administrative credentials live only for the lifetime of this window.
internal sealed class AdminForm : Form
{
    private readonly AppSettings settings;
    private RelayClient? client;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TextBox credential = new() { UseSystemPasswordChar = true, Width = 310 };
    private readonly TextBox name = new() { Width = 130, PlaceholderText = "展示名称" };
    private readonly TextBox user = new() { Width = 250, PlaceholderText = "唯一用户 ID（筛选 / 添加到现有身份）" };
    private readonly TextBox phrase = new() { UseSystemPasswordChar = true, Width = 180, PlaceholderText = "可选自定义口令（至少12字）" };
    private readonly ComboBox role = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 65 };
    private readonly ComboBox kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly ComboBox view = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly Label message = new() { AutoSize = true, MaximumSize = new Size(900, 0) };
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly TextBox detail = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false };
    private readonly TextBox bodyText = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly PictureBox bodyImage = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly FlowLayoutPanel actions = new() { AutoSize = true, Dock = DockStyle.Top, Enabled = false };
    private readonly List<JsonElement> rows = [];
    private readonly System.Windows.Forms.Timer sessionTimer = new() { Interval = 600000 };
    private double? cursor;
    private bool busy;

    public AdminForm(AppSettings settings)
    {
        this.settings = settings;
        Text = "Paperang Studio · C 权限管理"; ClientSize = new Size(1050, 800);
        MinimumSize = new Size(900, 650); Font = new Font("Microsoft YaHei UI", 9); StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1, Padding = new Padding(14) };
        root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.AutoSize));
        var login = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        login.Controls.Add(new Label { Text = "C 密钥 / 完整口令", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }); login.Controls.Add(credential);
        Button Add(FlowLayoutPanel panel, string title, Func<Task> run)
        {
            var button = new Button { Text = title, AutoSize = true };
            button.Click += async (_, _) => await Work(run); panel.Controls.Add(button); return button;
        }
        Add(login, "验证 C 权限", Login); Add(login, "私钥文件登录…", PrivateLogin); Add(login, "生成私钥…", GenerateKey);
        root.Controls.Add(login);
        root.Controls.Add(new Label { Text = "管理凭证仅保留在窗口内存。打印后台继续使用独立 M 凭证。导出文件含私人任务内容，请自行保管。", AutoSize = true });
        view.Items.AddRange(["凭证", "身份", "浏览器会话", "任务历史", "审计记录"]); view.SelectedIndex = 0;
        role.Items.AddRange(["A", "B", "C", "M"]); role.SelectedIndex = 0;
        kind.Items.AddRange(["token", "passphrase"]); kind.SelectedIndex = 0;
        actions.Controls.AddRange([view, name, user, role, kind, phrase]);
        Add(actions, "刷新", () => RefreshRows(false)); Add(actions, "下一页", () => RefreshRows(true));
        Add(actions, "新建凭证", Create); Add(actions, "保存名称", Rename); Add(actions, "轮换凭证", Rotate); Add(actions, "吊销所选", Revoke);
        Add(actions, "登记公钥…", TrustKey); Add(actions, "导出筛选历史…", Export); Add(actions, "确认纸面完成", Confirm);
        Add(actions, "锁定管理面板", () => { Lock(); return Task.CompletedTask; });
        root.Controls.Add(actions);
        root.Controls.Add(new Label { Text = "A：只投递 · B：投递和本身份状态 · C：全部管理 · M：限定设备执行。名称修改影响同一身份，历史名称保持快照。", AutoSize = true });
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 230 };
        var contents = new TabControl { Dock = DockStyle.Fill };
        var recordPage = new TabPage("完整记录"); recordPage.Controls.Add(detail);
        var textPage = new TabPage("任务正文"); textPage.Controls.Add(bodyText);
        var imagePage = new TabPage("任务图片"); imagePage.Controls.Add(bodyImage);
        contents.TabPages.AddRange([recordPage, textPage, imagePage]);
        split.Panel1.Controls.Add(grid); split.Panel2.Controls.Add(contents); root.Controls.Add(split); root.Controls.Add(message); Controls.Add(root);
        grid.SelectionChanged += (_, _) => {
            ClearDetails();
            var i = grid.CurrentRow?.Index ?? -1;
            if (i < 0 || i >= rows.Count) return;
            var row = rows[i]; detail.Text = JsonSerializer.Serialize(row, RelayClient.Json);
            if (view.SelectedIndex != 3) return;
            bodyText.Text = row.TryGetProperty("text", out var text) ? text.GetString() : "";
            try { if (row.Deserialize<RelayJob>(RelayClient.Json)?.Validate().Image() is { } raster) bodyImage.Image = NoteRenderer.Preview(raster); }
            catch { bodyText.AppendText("\r\n图片校验失败；已阻止预览。"); }
        };
        view.SelectedIndexChanged += async (_, _) => { if (client != null && !busy) await Work(() => RefreshRows(false)); };
        FormClosed += (_, _) => { lifetime.Cancel(); client?.Dispose(); sessionTimer.Dispose(); credential.Clear(); phrase.Clear(); ClearDetails(); rows.Clear(); lifetime.Dispose(); };
        sessionTimer.Tick += (_, _) => Lock();
        message.Text = "请使用 C 凭证，或已在服务器登记的所有者私钥。首次登记可在服务器执行 trust-public-key。";
    }
    private async Task Work(Func<Task> action)
    {
        if (busy) return; busy = true; UseWaitCursor = true;
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (RelayHttpException e) { if (IsDisposed) return; if (e.Status is 401 or 403) Lock(); message.Text = e.Message; }
        catch (Exception e) { if (IsDisposed) return; message.Text = e is ArgumentException or InvalidDataException or CryptographicException ? e.Message : "操作失败；请检查网络、服务器版本和所选数据。"; }
        finally { busy = false; if (!IsDisposed) UseWaitCursor = false; }
    }
    private RelayClient New(string token) => new(settings with { DeviceKey = token });
    private async Task Login()
    {
        using var login = New("psr_unauthenticated");
        // Login endpoint ignores bearer; use the anonymous header-free request mode.
        var result = await login.RequestAsync(HttpMethod.Post, "v2/auth/login", new { credential = credential.Text.Trim(), native = true }, lifetime.Token,
            new Dictionary<string,string> { ["X-Paperang-Native"] = "1" });
        await Accept(result.GetProperty("token").GetString()!);
        credential.Clear();
    }
    private async Task Accept(string token)
    {
        var next = New(token);
        try
        {
            var me = await next.Get("v2/auth/me", lifetime.Token);
            if (me.GetProperty("role").GetString() != "C") throw new InvalidDataException("此凭证没有 C 管理权限。");
            client?.Dispose(); client = next; sessionTimer.Stop(); sessionTimer.Start(); actions.Enabled = true; message.Text = "C 权限已验证。";
            await RefreshRows(false);
        }
        catch { if (client != next) next.Dispose(); throw; }
    }
    private async Task PrivateLogin()
    {
        using var pick = new OpenFileDialog { Filter = "Paperang 所有者文件|*.json", Title = "选择所有者私钥文件" };
        if (pick.ShowDialog(this) != DialogResult.OK) return;
        if (new FileInfo(pick.FileName).Length > 32768) throw new InvalidDataException("私钥文件过大");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(pick.FileName, lifetime.Token));
        if (doc.RootElement.GetProperty("server").GetString()?.TrimEnd('/') != settings.Server.TrimEnd('/')) throw new InvalidDataException("私钥文件绑定的服务器不匹配");
        using var rsa = RSA.Create(); rsa.ImportFromPem(doc.RootElement.GetProperty("privateKeyPem").GetString());
        using var login = New("psr_unauthenticated");
        var challenge = await login.Post("v2/auth/challenge", new { id = doc.RootElement.GetProperty("id").GetString() }, lifetime.Token);
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(challenge.GetProperty("message").GetString()!), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var proof = await login.Post("v2/auth/prove", new { challenge = challenge.GetProperty("challenge").GetString(), signature = Convert.ToBase64String(signature) }, lifetime.Token);
        await Accept(proof.GetProperty("token").GetString()!);
        message.Text = "所有者私钥已验证；管理会话有效期 10 分钟，超时后重新登录。";
    }
    private async Task GenerateKey()
    {
        using var pick = new SaveFileDialog { Filter = "Paperang 所有者文件|*.json", FileName = "paperang-owner.json" };
        if (pick.ShowDialog(this) != DialogResult.OK) return;
        using var rsa = RSA.Create(3072);
        var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        string id = "REPLACE_WITH_SERVER_CREDENTIAL_ID";
        if (client != null)
        {
            var result = await client.Post("v2/admin/public-keys", new { name = string.IsNullOrWhiteSpace(name.Text) ? "Windows owner" : name.Text, public_key = publicKey }, lifetime.Token);
            id = result.GetProperty("id").GetString()!;
        }
        WritePrivate(pick.FileName, JsonSerializer.Serialize(new { server = settings.Server.TrimEnd('/'), id, privateKeyPem = rsa.ExportPkcs8PrivateKeyPem() }, RelayClient.Json));
        await File.WriteAllTextAsync(pick.FileName + ".public.pem", publicKey, lifetime.Token);
        message.Text = client != null ? "私钥文件和公钥已保存；请用“私钥文件登录”验证持有权。" : "请在服务器登记 .public.pem，再将返回的 id 填入 JSON。私钥不要上传。";
    }
    private JsonElement Selected() { int i = grid.CurrentRow?.Index ?? -1; if (i < 0 || i >= rows.Count) throw new InvalidDataException("请先选择一行"); return rows[i]; }
    private async Task RefreshRows(bool more)
    {
        if (client == null) return;
        JsonElement result; string property;
        switch (view.SelectedIndex)
        {
            case 1: result = await client.Post("v2/admin/identities/search", new { before = more ? cursor ?? 1e20 : 1e20 }, lifetime.Token); property = "identities"; break;
            case 2: result = await client.Post("v2/admin/sessions/search", new { before = more ? cursor ?? 1e20 : 1e20 }, lifetime.Token); property = "sessions"; break;
            case 3: result = await client.Post("v2/admin/jobs/search", new { user_id = user.Text.Trim(), before = more ? cursor ?? 1e20 : 1e20 }, lifetime.Token); property = "jobs"; break;
            case 4: result = await client.Post("v2/admin/audit", new { before = more && cursor.HasValue ? (long)cursor.Value : long.MaxValue }, lifetime.Token); property = "events"; break;
            default: result = await client.Post("v2/admin/keys/search", new { before = more ? cursor ?? 1e20 : 1e20 }, lifetime.Token); property = "keys"; break;
        }
        cursor = result.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.Number ? next.GetDouble() : null;
        rows.Clear(); rows.AddRange(result.GetProperty(property).EnumerateArray().Select(x => x.Clone()));
        grid.Columns.Clear(); grid.Rows.Clear(); ClearDetails();
        var fields = view.SelectedIndex switch { 1 => new[] { "id", "name", "anonymous", "revoked" }, 2 => new[] { "credential_id", "browser_id", "expires" }, 3 => new[] { "id", "user_id", "state", "created" }, 4 => new[] { "id", "at", "actor", "action", "target" }, _ => new[] { "id", "name", "role", "kind", "revoked" } };
        foreach (var field in fields) grid.Columns.Add(field, field);
        foreach (var row in rows) grid.Rows.Add(fields.Select(f => row.TryGetProperty(f, out var value) ? value.ToString() : "").Cast<object>().ToArray());
        message.Text = $"已读取 {rows.Count} 条；历史按页获取，内容仅保留当前页。";
    }
    private async Task ShowSecret(Task<JsonElement> operation)
    {
        var result = await operation; phrase.Clear();
        // Show once in a separate native dialog; never persist admin-issued secrets.
        using var dialog = new Form { Text = "新凭证 · 仅展示本次", Size = new Size(700, 200), StartPosition = FormStartPosition.CenterParent };
        dialog.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, Text = result.ToString(), ScrollBars = ScrollBars.Vertical }); dialog.ShowDialog(this);
        await RefreshRows(false);
    }
    private Task Create() => ShowSecret(client!.Post("v2/admin/keys", new { name = name.Text.Trim(), role = role.Text, kind = kind.Text, password = string.IsNullOrEmpty(phrase.Text) ? null : phrase.Text, user_id = string.IsNullOrWhiteSpace(user.Text) ? null : user.Text.Trim() }, lifetime.Token));
    private async Task Rename() { if (view.SelectedIndex != 0) throw new InvalidDataException("请在凭证页选择要修改展示名称的身份"); await client!.Post($"v2/admin/keys/{Selected().GetProperty("id").GetString()}/rename", new { name = name.Text.Trim() }, lifetime.Token); await RefreshRows(false); }
    private Task Rotate() { if (view.SelectedIndex != 0) throw new InvalidDataException("请在凭证页选择要轮换的凭证"); return ShowSecret(client!.Post($"v2/admin/keys/{Selected().GetProperty("id").GetString()}/rotate", new { password = string.IsNullOrEmpty(phrase.Text) ? null : phrase.Text }, lifetime.Token)); }
    private async Task Revoke()
    {
        var row = Selected(); string path; object body = new { };
        if (view.SelectedIndex == 0) path = $"v2/admin/keys/{row.GetProperty("id").GetString()}/revoke";
        else if (view.SelectedIndex == 1) path = $"v2/admin/identities/{row.GetProperty("id").GetString()}/revoke";
        else if (view.SelectedIndex == 2) { path = "v2/admin/sessions/revoke"; body = new { credential_id = row.GetProperty("credential_id").GetString() }; }
        else throw new InvalidDataException("请在凭证、身份或会话页操作");
        if (MessageBox.Show(this, "吊销后将拒绝后续访问。吊销身份还会拒绝排队中的任务。继续？", "吊销", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        await client!.Post(path, body, lifetime.Token); await RefreshRows(false);
    }
    private async Task TrustKey()
    {
        using var pick = new OpenFileDialog { Filter = "公钥 PEM|*.pem" };
        if (pick.ShowDialog(this) != DialogResult.OK) return;
        if (new FileInfo(pick.FileName).Length > 16384) throw new InvalidDataException("公钥文件过大");
        var result = await client!.Post("v2/admin/public-keys", new { name = name.Text.Trim(), public_key = await File.ReadAllTextAsync(pick.FileName, lifetime.Token) }, lifetime.Token);
        detail.Text = result.ToString(); message.Text = "公钥已登记；需用对应私钥证明持有权。请记录返回的 id。";
    }
    private async Task Export()
    {
        using var pick = new SaveFileDialog { Filter = "JSON Lines（包含 raster1 附件）|*.jsonl", FileName = "paperang-history.jsonl" };
        if (pick.ShowDialog(this) != DialogResult.OK) return;
        await using var file = new FileStream(pick.FileName, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);
        await using var writer = new StreamWriter(file, new UTF8Encoding(false));
        string filter = user.Text.Trim();
        double before = 1e20; int count = 0;
        while (true)
        {
            var result = await client!.Post("v2/admin/jobs/export", new { user_id = filter, before }, lifetime.Token);
            var jobs = result.GetProperty("jobs").EnumerateArray().ToArray();
            if (jobs.Length == 0) break;
            foreach (var job in jobs) { await writer.WriteLineAsync(job.GetRawText()); count++; }
            var next = result.GetProperty("next").GetDouble(); if (next >= before) throw new InvalidDataException("服务器分页游标没有前进"); before = next;
        }
        message.Text = $"已导出 {count} 个任务；JSONL 包含正文、投递信息、事件与 base64 图片。";
    }
    private async Task Confirm()
    {
        if (view.SelectedIndex != 3) throw new InvalidDataException("请在历史页选择已发送的任务");
        if (MessageBox.Show(this, "确认你已检查实际纸面且内容完整？", "纸面确认", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        await client!.Post($"v2/admin/jobs/{Selected().GetProperty("id").GetString()}/confirm", new { }, lifetime.Token); await RefreshRows(false);
    }
    private void ClearDetails() { detail.Clear(); bodyText.Clear(); var old = bodyImage.Image; bodyImage.Image = null; old?.Dispose(); }
    internal static void WritePrivate(string path, string text)
    {
        var owner = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("无法识别当前 Windows 用户");
        var acl = new FileSecurity(); acl.SetOwner(owner); acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
        using var file = new FileInfo(path).Create(FileMode.Create, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.None, acl);
        var bytes = Encoding.UTF8.GetBytes(text); file.Write(bytes); file.Flush(true);
    }
    private void Lock() { sessionTimer.Stop(); client?.Dispose(); client = null; actions.Enabled = false; credential.Clear(); phrase.Clear(); rows.Clear(); grid.Rows.Clear(); ClearDetails(); message.Text = "管理面板已锁定。"; }
}
