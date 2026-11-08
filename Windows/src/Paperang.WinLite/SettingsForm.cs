using Paperang.Core;

namespace Paperang.WinLite;

internal sealed class SettingsForm : Form
{
    private readonly RelayEngine engine;
    private AppSettings initial;
    private readonly Func<AppSettings, bool, Task> save;
    private readonly Action<AppSettings> savePrinting;
    private readonly TextBox url = new(), key = new() { UseSystemPasswordChar = true };
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
    private bool printDirty;
    private bool saving;
    private readonly CancellationTokenSource lifetime = new();
    public SettingsForm(AppSettings initial, RelayEngine engine, Func<AppSettings, bool, Task> save, Action<List<RelayTemplate>, string> saveTemplates, Action<AppSettings> savePrinting)
    {
        this.initial = initial; this.engine = engine; this.save = save;
        this.savePrinting = savePrinting;
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
        tabs.TabPages.AddRange([connection.Page, printing.Page, general.Page]);
        root.Controls.Add(tabs);
        Add(connection.Body, "中转服务器根地址", url);
        Add(connection.Body, "设备密钥（psr_ 开头）", key);
        var show = new CheckBox { Text = "显示密钥", AutoSize = true }; show.CheckedChanged += (_, _) => key.UseSystemPasswordChar = !show.Checked;
        Add(connection.Body, "", show); Add(connection.Body, "", http);
        Add(connection.Body, "本地 P2 打印机", printers);
        var scanRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        var all = new CheckBox { Text = "显示所有命名设备", AutoSize = true };
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
        Add(general.Body, "当前状态", status);
        Add(general.Body, "", Hint("凭证与待回报记录由 Windows 当前用户 DPAPI 加密，保存在：\n" + LocalStore.Root + "\n\n无主页面、无后台系统服务。开机自启使用当前用户登录启动项，无需管理员权限。请将程序放在固定目录后开启自启。\n\nV" + AppVersion.Current + " · © 2026 ELT Group"));
        var bottom = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 14, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        feedback.Text = "保存后设置立即生效，关闭窗口继续驻留托盘。";
        bottom.Controls.Add(feedback); bottom.Controls.Add(apply);
        root.Controls.Add(bottom); Controls.Add(root);
        url.Text = initial.Server; key.Text = initial.DeviceKey; http.Checked = initial.AllowHttp;
        autoConnect.Checked = initial.AutoConnect; autoPrint.Checked = initial.AutoAllowPrint; startup.Checked = StartupRegistration.Enabled;
        reconnect.Checked = initial.KeepReconnecting;
        style.SelectedItem = initial.Style; density.Value = Math.Clamp(initial.Density, 0, 100); feed.Value = Math.Clamp(initial.Feed, 0, 2000);
        senderField.Checked = initial.IncludeSender; timeField.Checked = initial.IncludeTime; idField.Checked = initial.IncludeId;
        if (ulong.TryParse(initial.PrinterAddress, System.Globalization.NumberStyles.HexNumber, null, out ulong address))
        { printers.Items.Add(new PrinterChoice(address, initial.PrinterName, 0)); printers.SelectedIndex = 0; }
        apply.Click += async (_, _) => await Save();
        connectHere.Click += async (_, _) => await Save();
        foreach (var input in new Control[] { style, template, density, feed, senderField, timeField, idField, autoPrint })
        {
            if (input is ComboBox c) c.SelectedIndexChanged += (_, _) => QueuePrintSave();
            else if (input is NumericUpDown n) n.ValueChanged += (_, _) => QueuePrintSave();
            else if (input is CheckBox b) b.CheckedChanged += (_, _) => QueuePrintSave();
        }
        autosave.Tick += (_, _) => { autosave.Stop(); PersistPrinting(); };
        tabs.SelectedIndex = 0;
        ActiveControl = url;
        FormClosing += (_, args) => { autosave.Stop(); if (!PersistPrinting()) args.Cancel = true; };
        FormClosed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); autosave.Dispose(); Icon?.Dispose(); };
        UpdateStatus();
    }
    private async Task Save()
    {
        if (saving) return;
        saving = true; apply.Enabled = connectHere.Enabled = false;
        autosave.Stop();
        try
        {
            var selected = printers.SelectedItem as PrinterChoice;
            var next = initial with {
                Server = url.Text.Trim(), DeviceKey = key.Text.Trim(), AllowHttp = http.Checked,
                PrinterAddress = selected?.Address.ToString("X12") ?? "", PrinterName = selected?.Name ?? "",
                AutoConnect = autoConnect.Checked, AutoAllowPrint = autoPrint.Checked, KeepReconnecting = reconnect.Checked,
                Style = style.SelectedItem as string ?? "便签", Density = (int)density.Value, Feed = (int)feed.Value,
                IncludeSender = senderField.Checked, IncludeTime = timeField.Checked, IncludeId = idField.Checked
                , Templates = templates, TemplateId = (template.SelectedItem as RelayTemplate)?.Id ?? ""
            };
            await save(next, startup.Checked);
            initial = next; printDirty = false;
            feedback.Text = "已加密保存，正在后台连接；可以关闭设置。";
        }
        catch (Exception e) { feedback.Text = e is ArgumentException or InvalidOperationException ? e.Message : "保存失败，请检查本地数据权限或凭证文件。"; }
        finally { saving = false; if (!IsDisposed) apply.Enabled = connectHere.Enabled = true; }
    }
    private void QueuePrintSave() { printDirty = true; autosave.Stop(); autosave.Start(); }
    private bool PersistPrinting()
    {
        if (!printDirty) return true;
        try
        {
            var value = initial with { Style = style.Text, TemplateId = (template.SelectedItem as RelayTemplate)?.Id ?? "", Templates = templates,
                Density = (int)density.Value, Feed = (int)feed.Value, IncludeSender = senderField.Checked,
                IncludeTime = timeField.Checked, IncludeId = idField.Checked, AutoAllowPrint = autoPrint.Checked };
            savePrinting(value); initial = value; printDirty = false;
            feedback.Text = "当前模板和打印设置已自动保存，下次启动会恢复。";
            return true;
        }
        catch { feedback.Text = "自动保存失败，请检查本地数据权限。"; return false; }
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
