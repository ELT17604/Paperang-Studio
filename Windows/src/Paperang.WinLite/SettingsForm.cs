using Paperang.Core;

namespace Paperang.WinLite;

internal sealed class SettingsForm : Form
{
    private readonly RelayEngine engine;
    private AppSettings initial;
    private readonly Func<AppSettings, bool, Task> save;
    private readonly Func<AppSettings, bool, string> savePreferences;
    private readonly LocalStore local = new();
    private UiPreferences ui;
    private readonly TextBox url = new(), key = new() { ReadOnly = true };
    private readonly CheckBox http = new() { Text = "允许 HTTP 测试连接（公网建议 HTTPS）", AutoSize = true };
    private readonly CheckBox autoConnect = new() { Text = "启动后自动连接服务器与记住的打印机", AutoSize = true };
    private readonly CheckBox autoPrint = new() { Text = "每次连接自动开启允许打印", AutoSize = true };
    private readonly CheckBox startup = new() { Text = "登录 Windows 时自动启动并驻留托盘", AutoSize = true };
    private readonly CheckBox reconnect = new() { Text = "持续重连：失败 5 次后等待 1 分钟，再尝试 5 次，循环直到成功", AutoSize = true };
    private readonly CheckBox senderField = new() { Text = "投稿身份", AutoSize = true }, timeField = new() { Text = "时间", AutoSize = true }, idField = new() { Text = "短编号", AutoSize = true };
    private readonly ComboBox printers = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
    private readonly ComboBox style = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox template = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private List<RelayTemplate> templates;
    private readonly NumericUpDown density = new() { Minimum = 0, Maximum = 100, Width = 90 };
    private readonly NumericUpDown feed = new() { Minimum = 0, Maximum = 2000, Width = 90 };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(580, 0), ForeColor = Color.DimGray };
    private readonly Label connectionStatus = new() { AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Color.DimGray };
    private readonly Label feedback = new() { AutoSize = true, MaximumSize = new Size(580, 0) };
    private readonly Button apply = new() { Text = "保存并连接", AutoSize = true };
    private readonly Button scan = new() { Text = "扫描 P2（12 秒）", AutoSize = true };
    private readonly Button connectHere = new() { Text = "保存并连接", AutoSize = true };
    private readonly System.Windows.Forms.Timer autosave = new() { Interval = 350 };
    private bool settingsDirty;
    private bool saving;
    private readonly CancellationTokenSource lifetime = new();
    public SettingsForm(AppSettings initial, RelayEngine engine, Func<AppSettings, bool, Task> save, Action<List<RelayTemplate>, string> saveTemplates, Func<AppSettings, bool, string> savePreferences)
    {
        SuspendLayout(); AutoScaleDimensions = new SizeF(96, 96);
        this.initial = initial; this.engine = engine; this.save = save;
        this.savePreferences = savePreferences; ui = local.LoadUi();
        templates = initial.Templates;
        Text = "Paperang Studio · 设置"; ClientSize = new Size(680, 790);
        MinimumSize = new Size(620, 660); AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9); StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(20) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(new Label { Text = "PAPERANG STUDIO  /  WIN LITE", AutoSize = true, Font = new Font(Font.FontFamily, 13, FontStyle.Bold), Margin = new Padding(0, 0, 0, 16) });
        connectionStatus.Margin = new Padding(0, 0, 0, 12);
        root.Controls.Add(connectionStatus);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var connection = Page("连接");
        var printing = Page("打印");
        var general = Page("启动与状态");
        var management = Page("管理");
        var openAdmin = new Button { Text = "打开图形化管理面板…", AutoSize = true };
        openAdmin.Click += (_, _) => {
            try {
                if (!PersistSettings()) return;
                using var form = new AdminForm(this.initial, path => { key.Text = path; return PersistSettings(); }); form.ShowDialog(this);
            }
            catch { feedback.Text = "请先保存有效的服务器连接设置。"; }
        };
        Add(management.Body, "C 权限管理", openAdmin);
        Add(management.Body, "", Hint("管理 A/B/C/M 凭证、身份、浏览器会话、公钥和完整任务历史；支持按用户 ID 筛选及 JSONL 导出。\n使用本地所有者私钥登录；后台连接和管理使用同一已证明的公钥身份。"));
        tabs.TabPages.AddRange([connection.Page, printing.Page, general.Page, management.Page]);
        root.Controls.Add(tabs);
        Add(connection.Body, "中转服务器根地址", url);
        Add(connection.Body, "所有者私钥文件（最高权限 · 仅保存在本机）", key);
        var chooseKey = new Button { Text = "选择已配对的私钥文件…", AutoSize = true };
        chooseKey.Click += (_, _) => { using var pick = new OpenFileDialog { Filter = "Paperang 所有者文件|*.json" }; if (pick.ShowDialog(this) == DialogResult.OK) key.Text = pick.FileName; };
        Add(connection.Body, "", chooseKey); Add(connection.Body, "", http);
        Add(connection.Body, "", Hint("默认仅使用已配对的 RSA 所有者私钥。会话自动续期，管理与打印共用已验证的所有者身份。旧 M 设备密钥没有管理权限，不再作为默认连接凭证。"));
        Add(connection.Body, "本地 P2 打印机", printers);
        var scanRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        var all = new CheckBox { Text = "显示所有命名设备", AutoSize = true, Checked = ui.ShowAllPrinters };
        scanRow.Controls.AddRange([scan, all]); Add(connection.Body, "", scanRow);
        Add(connection.Body, "", connectHere);
        var forget = new Button { Text = "清除记住的打印机", AutoSize = true };
        forget.Click += (_, _) => { printers.Items.Clear(); printers.SelectedIndex = -1; };
        Add(connection.Body, "", forget);
        Add(connection.Body, "", Hint("请开启 P2 与电脑蓝牙。首次扫描后选择你的打印机；保存后只重连此设备。\n关闭设置窗口会继续在托盘收纸条。"));
        scan.Click += async (_, _) => {
            scan.Enabled = false; feedback.Text = "正在扫描附近蓝牙设备…";
            try
            {
                var rows = await BlePrinter.ScanAsync(all.Checked, lifetime.Token);
                var selected = printers.SelectedItem as PrinterChoice;
                printers.Items.Clear();
                if (selected != null && !rows.Any(x => x.Address == selected.Address)) printers.Items.Add(selected);
                printers.Items.AddRange(rows.Cast<object>().ToArray());
                if (selected != null) printers.SelectedItem = printers.Items.Cast<PrinterChoice>().FirstOrDefault(x => x.Address == selected.Address);
                if (printers.SelectedIndex < 0 && printers.Items.Count > 0) printers.SelectedIndex = 0;
                feedback.Text = rows.Length == 0 ? "未发现设备，请检查 P2 是否被其他客户端占用。" : $"发现 {rows.Length} 个设备，请选择你的 P2。";
            }
            catch (OperationCanceledException) { }
            catch { feedback.Text = "扫描失败，请检查 Windows 蓝牙设置及适配器。"; }
            finally { if (!IsDisposed) scan.Enabled = true; }
        };
        style.Items.AddRange(["便签", "小票", "复古电传", "访客纪念", "自定义"]);
        Add(printing.Body, "纸条样式", style);
        template.Items.AddRange(templates.Cast<object>().ToArray());
        template.SelectedIndex = Math.Max(0, templates.FindIndex(x => x.Id == initial.TemplateId));
        Add(printing.Body, "自定义模板", template);
        var edit = new Button { Text = "图形化样式编辑器…", AutoSize = true };
        edit.Click += (_, _) => {
            using var editor = new TemplateEditor(templates, (template.SelectedItem as RelayTemplate)?.Id ?? "");
            if (editor.ShowDialog(this) != DialogResult.OK) return;
            try {
                saveTemplates(editor.Templates, editor.TemplateId);
                templates = editor.Templates; template.Items.Clear(); template.Items.AddRange(templates.Cast<object>().ToArray());
                template.SelectedIndex = templates.FindIndex(x => x.Id == editor.TemplateId); style.SelectedItem = "自定义";
                feedback.Text = "模板已加密保存并应用；正在发送的任务保持原版式。";
            } catch { feedback.Text = "模板保存失败，请检查本地数据权限。"; }
        };
        Add(printing.Body, "", edit);
        Add(printing.Body, "打印浓度（0–100）", density); Add(printing.Body, "批后送纸（行）", feed);
        var fields = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top }; fields.Controls.AddRange([senderField, timeField, idField]);
        Add(printing.Body, "附加信息", fields); Add(printing.Body, "", autoPrint);
        style.SelectedIndexChanged += (_, _) => { fields.Enabled = style.Text != "自定义"; template.Enabled = style.Text == "自定义"; };
        Add(printing.Body, "", Hint("与 macOS 相同：576 点宽、浓度 100、批后送纸 150 行。\n支持文字与 raster1 黑白绘图；打印前检查纸仓并回读浓度。\n暂停只影响新任务，已进入打印机缓存的数据可能继续输出。\n“数据已发送”需要你核对实际纸面；不确定任务不会自动重打。"));
        Add(general.Body, "", autoConnect); Add(general.Body, "", startup); Add(general.Body, "", reconnect);
        var export = new Button { Text = "导出全部配置…", AutoSize = true };
        export.Click += (_, _) => ExportConfiguration();
        Add(general.Body, "配置备份", export);
        Add(general.Body, "", Hint("所有设置会自动保存在本机，包括私钥路径和界面偏好。导出 JSON 包含模板与设置，不包含私钥、密钥正文或会话。连接未填写完成时会保留草稿，后台继续使用原连接。"));
        Add(general.Body, "当前状态", status);
        Add(general.Body, "", Hint("凭证与待回报记录由 Windows 当前用户 DPAPI 加密，保存在：\n" + LocalStore.Root + "\n\n无主页面、无后台系统服务。开机自启使用当前用户登录启动项，无需管理员权限。请将程序放在固定目录后开启自启。\n\nV" + AppVersion.Current + " · © 2026 ELT Group"));
        var bottom = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 14, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        feedback.Text = "所有设置自动本地保存；连接变更可点击“保存并连接”应用。";
        bottom.Controls.Add(feedback); bottom.Controls.Add(apply);
        root.Controls.Add(bottom); Controls.Add(root);
        url.Text = initial.Server; key.Text = initial.OwnerKeyFile; http.Checked = initial.AllowHttp;
        autoConnect.Checked = initial.AutoConnect; autoPrint.Checked = initial.AutoAllowPrint; startup.Checked = initial.StartAtLogin ?? StartupRegistration.Enabled;
        reconnect.Checked = initial.KeepReconnecting;
        style.SelectedItem = initial.Style; density.Value = Math.Clamp(initial.Density, 0, 100); feed.Value = Math.Clamp(initial.Feed, 0, 2000);
        senderField.Checked = initial.IncludeSender; timeField.Checked = initial.IncludeTime; idField.Checked = initial.IncludeId;
        if (ulong.TryParse(initial.PrinterAddress, System.Globalization.NumberStyles.HexNumber, null, out ulong address))
        { printers.Items.Add(new PrinterChoice(address, initial.PrinterName, 0)); printers.SelectedIndex = 0; }
        apply.Click += async (_, _) => await Save();
        connectHere.Click += async (_, _) => await Save();
        foreach (var input in new Control[] { url, key, http, printers, autoConnect, startup, reconnect, style, template, density, feed, senderField, timeField, idField, autoPrint })
        {
            if (input is TextBox text) text.TextChanged += (_, _) => QueueSave();
            else if (input is ComboBox c) c.SelectedIndexChanged += (_, _) => QueueSave();
            else if (input is NumericUpDown n) n.ValueChanged += (_, _) => QueueSave();
            else if (input is CheckBox b) b.CheckedChanged += (_, _) => QueueSave();
        }
        autosave.Tick += (_, _) => { autosave.Stop(); PersistSettings(); };
        tabs.SelectedIndex = Math.Clamp(ui.SettingsTab, 0, tabs.TabCount - 1);
        tabs.SelectedIndexChanged += (_, _) => SaveUi(x => x with { SettingsTab = tabs.SelectedIndex });
        all.CheckedChanged += (_, _) => SaveUi(x => x with { ShowAllPrinters = all.Checked });
        ActiveControl = url;
        FormClosing += (_, args) => { autosave.Stop(); if (!PersistSettings()) args.Cancel = true; };
        FormClosed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); autosave.Dispose(); Icon?.Dispose(); };
        UpdateStatus(); ResumeLayout(true);
    }
    private async Task Save()
    {
        if (saving) return;
        saving = true; apply.Enabled = connectHere.Enabled = false;
        autosave.Stop();
        try
        {
            var next = ReadSettings();
            if (!PersistSettings()) return;
            await save(next, startup.Checked);
            initial = next; settingsDirty = false;
            feedback.Text = "已加密保存，正在后台连接；可以关闭设置。";
        }
        catch (Exception e) { feedback.Text = e is ArgumentException or InvalidOperationException ? e.Message : "保存失败，请检查本地数据权限或凭证文件。"; }
        finally { saving = false; if (!IsDisposed) apply.Enabled = connectHere.Enabled = true; }
    }
    private AppSettings ReadSettings()
    {
        var selected = printers.SelectedItem as PrinterChoice;
        return initial with {
            Server = url.Text.Trim(), OwnerKeyFile = key.Text.Trim(), AuthenticationMode = "owner", DeviceKey = "", AllowHttp = http.Checked,
            PrinterAddress = selected?.Address.ToString("X12") ?? "", PrinterName = selected?.Name ?? "",
            AutoConnect = autoConnect.Checked, AutoAllowPrint = autoPrint.Checked, KeepReconnecting = reconnect.Checked, StartAtLogin = startup.Checked,
            Style = style.SelectedItem as string ?? "便签", Density = (int)density.Value, Feed = (int)feed.Value,
            IncludeSender = senderField.Checked, IncludeTime = timeField.Checked, IncludeId = idField.Checked,
            Templates = templates, TemplateId = (template.SelectedItem as RelayTemplate)?.Id ?? ""
        };
    }
    private void SaveUi(Func<UiPreferences, UiPreferences> update)
    {
        try { ui = update(local.LoadUi()); local.SaveUi(ui); }
        catch { feedback.Text = "界面偏好保存失败，请检查本地数据权限。"; }
    }
    private void ExportConfiguration()
    {
        if (!PersistSettings()) return;
        using var pick = new SaveFileDialog { Filter = "Paperang 配置 JSON|*.json", FileName = "paperang-winlite-config.json", Title = "导出配置（含私钥路径，不含私钥文件）" };
        if (pick.ShowDialog(this) != DialogResult.OK) return;
        try {
            if (!string.IsNullOrWhiteSpace(key.Text) && string.Equals(Path.GetFullPath(pick.FileName), Path.GetFullPath(key.Text), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("配置导出不能覆盖私钥文件。");
            string temp = pick.FileName + ".tmp";
            File.WriteAllText(temp, ConfigurationExport.Serialize(ReadSettings(), local.LoadUi()), new System.Text.UTF8Encoding(false));
            File.Move(temp, pick.FileName, true);
            feedback.Text = "配置已导出（含模板和密钥路径）；私钥文件请单独保管。";
        } catch (InvalidOperationException e) { feedback.Text = e.Message; }
          catch { feedback.Text = "导出失败，请检查目标目录权限。"; }
    }
    private void QueueSave() { settingsDirty = true; autosave.Stop(); autosave.Start(); }
    private bool PersistSettings()
    {
        if (!settingsDirty) return true;
        try
        {
            var value = ReadSettings();
            feedback.Text = savePreferences(value, startup.Checked); initial = value; settingsDirty = false;
            return true;
        }
        catch { feedback.Text = "自动保存失败，请检查本地数据权限或启动项权限。"; return false; }
    }
    public void UpdateStatus()
    {
        if (IsDisposed) return;
        string countdown = engine.NextRetryAt is { } next ? $"（{Math.Max(0, (int)Math.Ceiling((next - DateTimeOffset.UtcNow).TotalSeconds))} 秒后重试）" : "";
        string value = engine.Message + countdown + "\n" + engine.PrinterStatus + "\n" + engine.PrintMessage + "\n自动打印：" + (engine.Allowed ? "开启" : "关闭");
        if (status.Text != value) status.Text = value;
        if (connectionStatus.Text != value) connectionStatus.Text = value;
    }
    private static (TabPage Page, TableLayoutPanel Body) Page(string title)
    {
        var page = new TabPage(title) { Padding = new Padding(16), AutoScroll = true, BackColor = SystemColors.Window };
        var body = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = 1 };
        page.Controls.Add(body); return (page, body);
    }
    private static void Add(TableLayoutPanel body, string label, Control control)
    {
        if (label.Length != 0) body.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 12, 0, 6) });
        control.Margin = new Padding(0, 4, 0, 6);
        if (control is TextBox or ComboBox) control.Dock = DockStyle.Top;
        body.Controls.Add(control);
    }
    private static Label Hint(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(550, 0), ForeColor = Color.DimGray, Margin = new Padding(0, 14, 0, 0) };
}
