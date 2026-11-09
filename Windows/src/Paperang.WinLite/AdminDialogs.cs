using System.Text.Json;
using Paperang.Core;

namespace Paperang.WinLite;

internal static class AdminDialogs
{
    internal sealed record IdentityOption(string Id, string Name)
    {
        public override string ToString() => $"{Name} · {Id[..Math.Min(8, Id.Length)]}";
    }
    internal sealed record CredentialInput(string Name, string Role, string Kind, string? UserId, string? Password);

    private static Form Dialog(string title) => new() { Text = title, ClientSize = new Size(570, 500), MinimumSize = new Size(570, 500), Font = new Font("Microsoft YaHei UI", 10), BackColor = Color.White, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi };
    private static TableLayoutPanel Layout(Form dialog, string title, string subtitle)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, AutoScroll = true };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = title, Font = new Font(dialog.Font.FontFamily, 19, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 10) });
        layout.Controls.Add(new Label { Text = subtitle, AutoSize = true, MaximumSize = new Size(510, 0), ForeColor = Color.DimGray, Margin = new Padding(0, 0, 0, 14) });
        dialog.Controls.Add(layout); return layout;
    }
    private static void Field(TableLayoutPanel panel, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 0, 4) });
        control.Dock = DockStyle.Top; control.Margin = new Padding(0, 0, 0, 6); panel.Controls.Add(control);
    }
    private static Button Footer(Form dialog, TableLayoutPanel panel, string title)
    {
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 16, 0, 0) };
        var submit = new Button { Text = title, AutoSize = true, Height = 36, Padding = new Padding(12, 4, 12, 4) };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel, Height = 36 };
        buttons.Controls.Add(submit); buttons.Controls.Add(cancel); panel.Controls.Add(buttons); dialog.AcceptButton = submit; dialog.CancelButton = cancel; return submit;
    }
    internal static string? AskName(IWin32Window owner, string title, string initial)
    {
        using var dialog = Dialog(title); dialog.ClientSize = new Size(570, 310); dialog.MinimumSize = new Size(570, 310);
        var panel = Layout(dialog, title, "展示名称便于识别；唯一 ID 由系统管理，无需输入。");
        var value = new TextBox { Text = initial, MaxLength = 80 }; Field(panel, "展示名称", value);
        var submit = Footer(dialog, panel, "保存");
        submit.Click += (_, _) => { if (string.IsNullOrWhiteSpace(value.Text)) { value.Focus(); return; } dialog.DialogResult = DialogResult.OK; };
        return dialog.ShowDialog(owner) == DialogResult.OK ? value.Text.Trim() : null;
    }
    internal static CredentialInput? CreateCredential(IWin32Window owner, IReadOnlyList<IdentityOption> identities)
    {
        using var dialog = Dialog("新建凭证"); dialog.ClientSize = new Size(570, 650); dialog.MinimumSize = new Size(570, 650);
        var panel = Layout(dialog, "新建凭证", "凭证 ID 自动生成。默认创建新身份并生成随机密钥，无需手填任何 ID。");
        var name = new TextBox { PlaceholderText = "例如：客厅投稿 / 自动化任务", MaxLength = 80 };
        var identity = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        identity.Items.Add("新身份（自动生成 ID）"); foreach (var item in identities) identity.Items.Add(item); identity.SelectedIndex = 0;
        var role = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        role.Items.AddRange(["A · 只投递（投件箱）", "B · 投递与本身份进度", "C · 全部管理", "M · 设备执行"]); role.SelectedIndex = 0;
        var kind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        kind.Items.AddRange(["随机密钥（自动生成）", "口令（留空自动生成）"]); kind.SelectedIndex = 0;
        var phrase = new TextBox { UseSystemPasswordChar = true, MaxLength = 0, PlaceholderText = "留空自动生成；自定义口令不限长度与复杂度", Enabled = false };
        Field(panel, "展示名称", name); Field(panel, "身份 · 可选择最近 100 个或当前所选身份", identity);
        Field(panel, "权限", role); Field(panel, "凭证类型", kind); Field(panel, "自定义口令（可选）", phrase);
        var validation = new Label { AutoSize = true, MaximumSize = new Size(510, 0), ForeColor = Color.Firebrick }; panel.Controls.Add(validation);
        role.SelectedIndexChanged += (_, _) => { if (role.SelectedIndex == 3) kind.SelectedIndex = 0; kind.Enabled = role.SelectedIndex != 3; };
        kind.SelectedIndexChanged += (_, _) => { phrase.Enabled = kind.SelectedIndex == 1; if (!phrase.Enabled) phrase.Clear(); };
        identity.SelectedIndexChanged += (_, _) => { if (identity.SelectedItem is IdentityOption item) name.Text = item.Name; };
        var submit = Footer(dialog, panel, "创建凭证");
        submit.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(name.Text)) { validation.Text = "请输入便于识别的展示名称。"; name.Focus(); return; }
            dialog.DialogResult = DialogResult.OK;
        };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return null;
        return new(name.Text.Trim(), new[] { "A", "B", "C", "M" }[role.SelectedIndex], kind.SelectedIndex == 0 ? "token" : "passphrase", (identity.SelectedItem as IdentityOption)?.Id, kind.SelectedIndex == 1 && phrase.Text.Length > 0 ? phrase.Text : null);
    }
    internal static void ShowCredential(IWin32Window owner, JsonElement result)
    {
        using var dialog = Dialog("凭证已生成 · 仅展示本次"); dialog.ClientSize = new Size(640, 480);
        var panel = Layout(dialog, "凭证已生成", "请现在复制并妥善保管。关闭后无法再次读取密钥；应用不会自动复制或保存明文。");
        string Value(string field) => result.TryGetProperty(field, out var value) ? value.ToString() : "";
        Field(panel, "凭证 / 完整口令", new TextBox { Text = Value("token"), ReadOnly = true, Multiline = true, Height = 76, ScrollBars = ScrollBars.Vertical });
        Field(panel, "凭证 ID（已自动生成）", new TextBox { Text = Value("id"), ReadOnly = true });
        Field(panel, "身份 ID（供归类与筛选）", new TextBox { Text = Value("user_id"), ReadOnly = true });
        var close = Footer(dialog, panel, "已保存，关闭"); close.Click += (_, _) => dialog.DialogResult = DialogResult.OK;
        dialog.ShowDialog(owner);
    }
}
