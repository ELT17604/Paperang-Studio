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
    private readonly TextBox user = new() { ReadOnly = true, Width = 250, PlaceholderText = "全部身份" };
    private readonly ComboBox view = new() { Visible = false };
    private readonly ListBox navigation = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false, ItemHeight = 46, Font = new Font("Microsoft YaHei UI", 11), BackColor = Color.FromArgb(239, 243, 249) };
    private readonly ComboBox pageSize = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 85 };
    private readonly CheckBox hideRevoked = new() { Text = "隐藏已吊销", Checked = true, AutoSize = true, Padding = new Padding(8, 7, 0, 0) };
    private readonly Label pageLabel = new() { AutoSize = true, Padding = new Padding(8, 8, 8, 0) };
    private readonly Label heading = new() { AutoSize = true, Font = new Font("Microsoft YaHei UI", 17, FontStyle.Bold), ForeColor = Color.FromArgb(26, 43, 69) };
    private readonly Label message = new() { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(10), ForeColor = Color.FromArgb(65, 83, 107) };
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, AutoGenerateColumns = false };
    private readonly TextBox detail = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, BorderStyle = BorderStyle.None, BackColor = Color.White };
    private readonly TextBox bodyText = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Color.White };
    private readonly PictureBox bodyImage = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly FlowLayoutPanel actions = new() { AutoSize = true, Dock = DockStyle.Fill, Enabled = false, Padding = new Padding(0, 6, 0, 6) };
    private readonly FlowLayoutPanel filters = new() { AutoSize = true, Dock = DockStyle.Fill, Enabled = false };
    private readonly FlowLayoutPanel paging = new() { AutoSize = true, Dock = DockStyle.Fill, Enabled = false };
    private readonly List<(Button Button, int[] Views)> contextual = [];
    private Button previous = null!, nextPage = null!;
    private sealed record Page(List<JsonElement> Items, double? After, List<JsonElement> Remainder);
    private readonly List<Page> pages = [];
    private int pageIndex = -1, firstPageNumber = 1;
    private readonly List<JsonElement> rows = [];
    private readonly System.Windows.Forms.Timer sessionTimer = new() { Interval = 600000 };
    private bool busy;

    public AdminForm(AppSettings settings)
    {
        SuspendLayout(); AutoScaleDimensions = new SizeF(96, 96);
        this.settings = settings;
        Text = "Paperang Studio · 管理中心"; ClientSize = new Size(1180, 780);
        MinimumSize = new Size(1040, 700); Font = new Font("Microsoft YaHei UI", 9.5f); AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent; BackColor = Color.FromArgb(246, 248, 252);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(20) };
        root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.AutoSize));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 0, 0, 18) };
        header.ColumnStyles.Add(new(SizeType.Percent, 100)); header.ColumnStyles.Add(new(SizeType.AutoSize));
        var title = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill };
        title.Controls.Add(new Label { Text = "管理中心", AutoSize = true, Font = new Font(Font.FontFamily, 23, FontStyle.Bold), ForeColor = Color.FromArgb(26, 43, 69) });
        title.Controls.Add(new Label { Text = "所有者私钥 · 安全连接   /   " + settings.Server, AutoSize = true, ForeColor = Color.DimGray });
        var login = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right };
        AddButton(login, "切换私钥…", PrivateLogin); AddButton(login, "生成并配对私钥…", GenerateKey);
        AddButton(login, "锁定", () => { Lock(); return Task.CompletedTask; });
        header.Controls.Add(title, 0, 0); header.Controls.Add(login, 1, 0); root.Controls.Add(header, 0, 0);
        var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        workspace.ColumnStyles.Add(new(SizeType.Absolute, 160)); workspace.ColumnStyles.Add(new(SizeType.Percent, 100));
        string[] categories = ["凭证", "身份", "浏览器会话", "任务历史", "审计记录"];
        view.Items.AddRange(categories); view.SelectedIndex = 0;
        navigation.DrawMode = DrawMode.OwnerDrawFixed;
        navigation.DrawItem += (_, e) => {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var brush = new SolidBrush(selected ? Color.FromArgb(219, 231, 250) : navigation.BackColor); e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, categories[e.Index], navigation.Font, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height), selected ? Color.FromArgb(25, 70, 135) : Color.FromArgb(65, 83, 107), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
        navigation.Items.AddRange(categories); navigation.SelectedIndex = 0;
        var sidebar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 20, 10, 10), BackColor = navigation.BackColor, Margin = new Padding(0, 0, 18, 0) };
        sidebar.Controls.Add(navigation); workspace.Controls.Add(sidebar, 0, 0);
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Color.White, Padding = new Padding(16) };
        main.RowStyles.Add(new(SizeType.AutoSize)); main.RowStyles.Add(new(SizeType.AutoSize)); main.RowStyles.Add(new(SizeType.AutoSize)); main.RowStyles.Add(new(SizeType.Percent, 100)); main.RowStyles.Add(new(SizeType.AutoSize));
        heading.Text = "凭证"; main.Controls.Add(heading, 0, 0);
        void Context(string text, Func<Task> run, params int[] views) { var button = AddButton(actions, text, run); contextual.Add((button, views)); }
        Context("＋ 新建凭证", Create, 0, 1); Context("修改展示名称", Rename, 0); Context("轮换…", Rotate, 0);
        Context("吊销…", Revoke, 0, 1, 2); Context("登记公钥…", TrustKey, 0);
        Context("查看此身份的任务", FilterSelected, 0, 1); Context("导出历史…", Export, 3); Context("确认纸面完成", Confirm, 3);
        main.Controls.Add(actions, 0, 1);
        filters.Controls.Add(hideRevoked); filters.Controls.Add(new Label { Text = "任务身份", AutoSize = true, Padding = new Padding(8, 8, 0, 0), Name = "filterLabel" });
        filters.Controls.Add(user); var clearFilter = AddButton(filters, "清除筛选", async () => { user.Clear(); await RefreshRows(false); }); clearFilter.Name = "clearFilter";
        main.Controls.Add(filters, 0, 2);
        grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersHeight = 40; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(239, 243, 249), ForeColor = Color.FromArgb(47, 65, 89), Font = new Font(Font, FontStyle.Bold), Padding = new Padding(6) };
        grid.DefaultCellStyle = new DataGridViewCellStyle { Padding = new Padding(6, 3, 6, 3), SelectionBackColor = Color.FromArgb(224, 235, 252), SelectionForeColor = Color.FromArgb(24, 55, 98) };
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 254); grid.RowTemplate.Height = 38;
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Size = new Size(900, 480), SplitterDistance = 285, Panel1MinSize = 130, Panel2MinSize = 100, SplitterWidth = 8, BackColor = Color.FromArgb(246, 248, 252) };
        var contents = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 6) };
        var recordPage = new TabPage("记录详情") { Padding = new Padding(12) }; recordPage.Controls.Add(detail);
        var textPage = new TabPage("任务正文") { Padding = new Padding(12) }; textPage.Controls.Add(bodyText);
        var imagePage = new TabPage("任务图片"); imagePage.Controls.Add(bodyImage);
        contents.TabPages.AddRange([recordPage, textPage, imagePage]); split.Panel1.Controls.Add(grid); split.Panel2.Controls.Add(contents); main.Controls.Add(split, 0, 3);
        paging.Controls.Add(new Label { Text = "每页", AutoSize = true, Padding = new Padding(0, 8, 3, 0) });
        pageSize.Items.AddRange([25, 50, 100]); pageSize.SelectedIndex = 0; paging.Controls.Add(pageSize);
        previous = AddButton(paging, "上一页", async () => { if (pageIndex > 0) { pageIndex--; await DisplayPage(); } });
        nextPage = AddButton(paging, "下一页", () => RefreshRows(true)); paging.Controls.Add(pageLabel); AddButton(paging, "刷新", () => RefreshRows(false)); main.Controls.Add(paging, 0, 4);
        workspace.Controls.Add(main, 1, 0); root.Controls.Add(workspace, 0, 1); root.Controls.Add(message, 0, 2); Controls.Add(root);
        grid.SelectionChanged += async (_, _) => { if (!busy && client != null) await Work(LoadSelected); };
        navigation.SelectedIndexChanged += async (_, _) => { view.SelectedIndex = navigation.SelectedIndex; UpdateContext(); if (client != null) await Work(() => RefreshRows(false)); };
        pageSize.SelectedIndexChanged += async (_, _) => { if (client != null) await Work(() => RefreshRows(false)); };
        hideRevoked.CheckedChanged += async (_, _) => { if (client != null) await Work(() => RefreshRows(false)); };
        FormClosed += (_, _) => { lifetime.Cancel(); client?.Dispose(); sessionTimer.Dispose(); ClearDetails(); rows.Clear(); pages.Clear(); lifetime.Dispose(); };
        Shown += async (_, _) => { if (File.Exists(settings.OwnerKeyFile)) await Work(() => LoginOwnerFile(settings.OwnerKeyFile)); };
        sessionTimer.Tick += (_, _) => { if (!busy) Lock(); };
        UpdateContext(); message.Text = "请选择已配对的所有者私钥。密钥留在本机，短期会话仅保存在内存。";
        ResumeLayout(true);
    }
    private Button AddButton(FlowLayoutPanel panel, string title, Func<Task> run)
    {
        var button = new Button { Text = title, AutoSize = true, MinimumSize = new Size(72, 34), Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 3, 8, 3), FlatStyle = FlatStyle.Flat, BackColor = Color.White };
        button.FlatAppearance.BorderColor = Color.FromArgb(211, 220, 232);
        button.Click += async (_, _) => await Work(run); panel.Controls.Add(button); return button;
    }
    private void UpdateContext()
    {
        heading.Text = view.Text;
        foreach (var item in contextual) item.Button.Visible = item.Views.Contains(view.SelectedIndex);
        hideRevoked.Visible = view.SelectedIndex is 0 or 1;
        foreach (Control control in filters.Controls) if (control != hideRevoked) control.Visible = view.SelectedIndex == 3;
        filters.Visible = view.SelectedIndex is 0 or 1 or 3;
    }
    private async Task Work(Func<Task> action)
    {
        if (busy) return; busy = true; UseWaitCursor = true; actions.Enabled = filters.Enabled = paging.Enabled = false; navigation.Enabled = grid.Enabled = false;
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (RelayHttpException e) { if (IsDisposed) return; if (e.Status is 401 or 403) Lock(); message.Text = e.Message; }
        catch (Exception e) { if (IsDisposed) return; message.Text = e is ArgumentException or InvalidDataException or CryptographicException ? e.Message : "操作失败；请检查网络、服务器版本和所选数据。"; }
        finally { busy = false; if (!IsDisposed) { UseWaitCursor = false; actions.Enabled = filters.Enabled = paging.Enabled = client != null; navigation.Enabled = grid.Enabled = true; } }
    }
    private async Task PrivateLogin()
    {
        using var pick = new OpenFileDialog { Filter = "Paperang 所有者文件|*.json", Title = "选择所有者私钥文件" };
        if (pick.ShowDialog(this) != DialogResult.OK) return;
        await LoginOwnerFile(pick.FileName);
    }
    private async Task LoginOwnerFile(string path)
    {
        using var authorized = new RelayClient(settings with { OwnerKeyFile = path, AuthenticationMode = "owner", DeviceKey = "" });
        var me = await authorized.Get("v2/auth/me", lifetime.Token);
        if (me.GetProperty("role").GetString() != "C") throw new InvalidDataException("私钥未获得 C 权限");
        // Each management request can renew its proof session without storing a token.
        client?.Dispose(); client = new RelayClient(settings with { OwnerKeyFile = path, AuthenticationMode = "owner", DeviceKey = "" });
        actions.Enabled = filters.Enabled = paging.Enabled = true; sessionTimer.Stop(); sessionTimer.Start(); await RefreshRows(false);
        message.Text = "所有者私钥已验证；后台使用同一公钥身份，会话自动续期。";
    }
    private async Task GenerateKey()
    {
        if (client == null) throw new InvalidDataException("请先使用已配对的所有者私钥解锁，再生成并自动配对新私钥。");
        string? displayName = AdminDialogs.AskName(this, "生成并配对私钥", "新的所有者"); if (displayName == null) return;
        using var pick = new SaveFileDialog { Filter = "Paperang 所有者文件|*.json", FileName = "paperang-owner.json" };
        if (pick.ShowDialog(this) != DialogResult.OK) return;
        if (File.Exists(pick.FileName)) throw new InvalidDataException("请选择新的文件名，避免覆盖现有私钥。");
        using var rsa = RSA.Create(3072);
        var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        string id;
        {
            var result = await client.Post("v2/admin/public-keys", new { name = displayName, public_key = publicKey }, lifetime.Token);
            id = result.GetProperty("id").GetString()!;
        }
        WritePrivate(pick.FileName, JsonSerializer.Serialize(new { server = settings.Server.TrimEnd('/'), id, privateKeyPem = rsa.ExportPkcs8PrivateKeyPem() }, RelayClient.Json));
        await File.WriteAllTextAsync(pick.FileName + ".public.pem", publicKey, lifetime.Token);
        message.Text = "私钥已生成并登记，ID 已自动写入文件。请用“切换私钥”验证；不要上传私钥。";
    }
    private JsonElement Selected() { int i = grid.CurrentRow?.Index ?? -1; if (i < 0 || i >= rows.Count) throw new InvalidDataException("请先选择一行"); return rows[i]; }
    private static bool Revoked(JsonElement row) => row.TryGetProperty("revoked", out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.Number && value.GetInt32() != 0);
    private async Task RefreshRows(bool more)
    {
        if (client == null) return;
        if (more && pageIndex + 1 < pages.Count) { pageIndex++; await DisplayPage(); return; }
        if (!more) { pages.Clear(); pageIndex = -1; firstPageNumber = 1; }
        var pending = pageIndex >= 0 ? new List<JsonElement>(pages[pageIndex].Remainder) : [];
        double? before = pageIndex >= 0 ? pages[pageIndex].After : 1e20;
        int limit = (int)pageSize.SelectedItem!;
        var items = new List<JsonElement>();
        // Also paginate correctly against older relays with a fixed server page size.
        // Keep only summaries for jobs; body/image are fetched for the selected record.
        for (int batch = 0; (items.Count < limit || pending.Count == 0 && before != null) && batch < 50; batch++)
        {
            int take = Math.Min(limit - items.Count, pending.Count);
            items.AddRange(pending.Take(take)); pending.RemoveRange(0, take);
            if (items.Count == limit && pending.Count > 0 || before == null) break;
            JsonElement result; string property;
            var query = new { before, limit, hide_revoked = hideRevoked.Checked };
            switch (view.SelectedIndex)
            {
                case 1: result = await client.Post("v2/admin/identities/search", query, lifetime.Token); property = "identities"; break;
                case 2: result = await client.Post("v2/admin/sessions/search", query, lifetime.Token); property = "sessions"; break;
                case 3: result = await client.Post("v2/admin/jobs/search", new { user_id = user.Text.Trim(), before, limit, summary = true }, lifetime.Token); property = "jobs"; break;
                case 4: result = await client.Post("v2/admin/audit", new { before = before >= 1e20 ? long.MaxValue : (long)before.Value, limit }, lifetime.Token); property = "events"; break;
                default: result = await client.Post("v2/admin/keys/search", query, lifetime.Token); property = "keys"; break;
            }
            var fetched = result.GetProperty(property).EnumerateArray().ToArray();
            var after = result.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.Number ? next.GetDouble() : (double?)null;
            if (after.HasValue && after >= before) throw new InvalidDataException("服务器分页游标没有前进");
            before = fetched.Length == 0 ? null : after;
            foreach (var row in fetched)
            {
                if (view.SelectedIndex is 0 or 1 && hideRevoked.Checked && Revoked(row)) continue;
                if (view.SelectedIndex == 3)
                {
                    string[] fields = ["id", "shortID", "user_id", "state", "created", "sender"];
                    pending.Add(JsonSerializer.SerializeToElement(row.EnumerateObject().Where(x => fields.Contains(x.Name)).ToDictionary(x => x.Name, x => x.Value.Clone())));
                }
                else pending.Add(row.Clone());
            }
        }
        pages.Add(new Page(items, before, pending)); pageIndex = pages.Count - 1;
        if (pages.Count > 12) { pages.RemoveAt(0); pageIndex--; firstPageNumber++; }
        await DisplayPage();
    }
    private async Task DisplayPage()
    {
        rows.Clear(); rows.AddRange(pages[pageIndex].Items); grid.Columns.Clear(); grid.Rows.Clear(); ClearDetails();
        var fields = view.SelectedIndex switch { 1 => new[] { "name", "anonymous", "revoked", "id" }, 2 => new[] { "credential_id", "browser_id", "expires" }, 3 => new[] { "shortID", "sender", "state", "created", "user_id" }, 4 => new[] { "at", "action", "actor", "target" }, _ => new[] { "name", "role", "kind", "revoked", "id" } };
        var labels = new Dictionary<string, string> { ["name"] = "展示名称", ["id"] = "唯一 ID", ["role"] = "权限", ["kind"] = "类型", ["revoked"] = "状态", ["anonymous"] = "身份类型", ["credential_id"] = "凭证 ID", ["browser_id"] = "浏览器身份", ["expires"] = "到期时间", ["shortID"] = "任务编号", ["sender"] = "投稿者", ["state"] = "进度", ["created"] = "创建时间", ["user_id"] = "身份 ID", ["at"] = "时间", ["action"] = "操作", ["actor"] = "操作人", ["target"] = "对象" };
        foreach (var field in fields) { int index = grid.Columns.Add(field, labels[field]); grid.Columns[index].SortMode = DataGridViewColumnSortMode.NotSortable; grid.Columns[index].FillWeight = field.EndsWith("id") ? 150 : 100; }
        foreach (var row in rows) grid.Rows.Add(fields.Select(f => DisplayValue(row, f)).Cast<object>().ToArray());
        previous.Enabled = pageIndex > 0; nextPage.Enabled = pages[pageIndex].After != null || pages[pageIndex].Remainder.Count > 0;
        pageLabel.Text = $"第 {firstPageNumber + pageIndex} 页 · 本页 {rows.Count} 条";
        message.Text = "选择记录查看详情。任务正文和图片按需读取；导出文件包含私人内容，请妥善保管。";
        await LoadSelected();
    }
    private static string DisplayValue(JsonElement row, string field)
    {
        if (!row.TryGetProperty(field, out var value)) return "—";
        if (field == "revoked") return Revoked(row) ? "已吊销" : row.TryGetProperty("kind", out var kind) && kind.GetString() == "pendingkey" ? "待验证" : "有效";
        if (field == "kind") return value.GetString() switch { "token" => "随机密钥", "passphrase" => "口令", "publickey" => "所有者公钥", "pendingkey" => "待验证公钥", _ => value.ToString() };
        if (field == "state") return value.GetString() switch { "queued" => "排队中", "claimed" => "已认领", "printing" => "打印中", "sent" => "已发送", "confirmed" => "纸面已确认", "unknown" => "结果待核实", "rejected" => "已拒绝", _ => value.ToString() };
        if (field == "anonymous") return value.ToString() is "1" or "True" or "true" ? "匿名" : "已命名";
        if (field is "created" or "expires" or "at" && value.TryGetDouble(out double seconds)) return DateTimeOffset.UnixEpoch.AddSeconds(seconds).ToLocalTime().ToString("MM-dd HH:mm");
        return value.ToString();
    }
    private async Task LoadSelected()
    {
        ClearDetails(); if (grid.CurrentRow == null || client == null) return;
        var row = Selected();
        if (view.SelectedIndex == 3)
        {
            var result = await client.Post("v2/admin/jobs/search", new { job_id = row.GetProperty("id").GetString(), limit = 1 }, lifetime.Token);
            if (!result.GetProperty("jobs").EnumerateArray().Any()) { detail.Text = "记录已不可用。"; return; }
            row = result.GetProperty("jobs")[0];
            bodyText.Text = row.TryGetProperty("text", out var text) ? text.GetString() : "";
            try { if (row.Deserialize<RelayJob>(RelayClient.Json)?.Validate().Image() is { } raster) bodyImage.Image = NoteRenderer.Preview(raster); }
            catch { bodyText.AppendText("\r\n图片校验失败；已阻止预览。"); }
        }
        detail.Text = JsonSerializer.Serialize(row, new JsonSerializerOptions(RelayClient.Json) { WriteIndented = true });
    }
    private Task FilterSelected()
    {
        var row = Selected(); user.Text = row.GetProperty(view.SelectedIndex == 1 ? "id" : "user_id").GetString();
        navigation.SelectedIndex = 3; view.SelectedIndex = 3; UpdateContext(); return RefreshRows(false);
    }
    private async Task ShowSecret(Task<JsonElement> operation)
    {
        var result = await operation;
        AdminDialogs.ShowCredential(this, result);
        await RefreshRows(false);
    }
    private async Task Create()
    {
        var options = new List<AdminDialogs.IdentityOption>();
        var selected = grid.CurrentRow?.Index ?? -1;
        if (selected >= 0 && selected < rows.Count && view.SelectedIndex is 0 or 1)
        {
            var row = rows[selected]; if (!Revoked(row)) options.Add(new(row.GetProperty(view.SelectedIndex == 1 ? "id" : "user_id").GetString()!, row.GetProperty("name").GetString() ?? "所选身份"));
        }
        var result = await client!.Post("v2/admin/identities/search", new { limit = 100, hide_revoked = true }, lifetime.Token);
        foreach (var row in result.GetProperty("identities").EnumerateArray()) if (!Revoked(row) && !options.Any(x => x.Id == row.GetProperty("id").GetString())) options.Add(new(row.GetProperty("id").GetString()!, row.GetProperty("name").GetString() ?? "未命名"));
        var input = AdminDialogs.CreateCredential(this, options);
        if (input == null) return;
        await ShowSecret(client.Post("v2/admin/keys", new { name = input.Name, role = input.Role, kind = input.Kind, password = input.Password, user_id = input.UserId }, lifetime.Token));
    }
    private async Task Rename()
    {
        var row = Selected(); string? value = AdminDialogs.AskName(this, "修改展示名称", row.GetProperty("name").GetString() ?? ""); if (value == null) return;
        await client!.Post($"v2/admin/keys/{row.GetProperty("id").GetString()}/rename", new { name = value }, lifetime.Token); await RefreshRows(false);
    }
    private async Task Rotate()
    {
        var row = Selected(); string type = row.GetProperty("kind").GetString() ?? "";
        if (type is "publickey" or "pendingkey") throw new InvalidDataException("公钥凭证请使用“生成并配对私钥”创建替代；验证后再吊销旧公钥。");
        if (MessageBox.Show(this, "将自动生成新凭证并使旧凭证失效。继续？", "轮换凭证", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        await ShowSecret(client!.Post($"v2/admin/keys/{row.GetProperty("id").GetString()}/rotate", new { }, lifetime.Token));
    }
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
        string? displayName = AdminDialogs.AskName(this, "登记公钥", Path.GetFileNameWithoutExtension(pick.FileName)); if (displayName == null) return;
        var result = await client!.Post("v2/admin/public-keys", new { name = displayName, public_key = await File.ReadAllTextAsync(pick.FileName, lifetime.Token) }, lifetime.Token);
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
    private void Lock() { sessionTimer.Stop(); client?.Dispose(); client = null; actions.Enabled = filters.Enabled = paging.Enabled = false; rows.Clear(); pages.Clear(); pageIndex = -1; grid.Rows.Clear(); ClearDetails(); message.Text = "管理面板已锁定。"; }
}
