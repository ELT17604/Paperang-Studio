using Paperang.Core;
using System.Text.Json;

namespace Paperang.WinLite;

internal sealed class TemplateEditor : Form
{
    public List<RelayTemplate> Templates { get; private set; }
    public string TemplateId => Current.Id;
    private readonly ComboBox templateList = new() { Width = 155, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox name = new() { Width = 150, MaxLength = 80 };
    private readonly ListBox blocks = new() { Dock = DockStyle.Fill, AllowDrop = true, IntegralHeight = false };
    private readonly CheckBox enabled = new() { Text = "启用区块", AutoSize = true }, bold = new() { Text = "粗体", AutoSize = true };
    private readonly ComboBox kind = new() { DropDownStyle = ComboBoxStyle.DropDownList }, font = new() { DropDownStyle = ComboBoxStyle.DropDown }, alignment = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox text = new() { Multiline = true, Height = 90, MaxLength = 2000, ScrollBars = ScrollBars.Vertical };
    private readonly NumericUpDown size = Number(8, 96), margin = Number(0, 120), gap = Number(0, 200);
    private readonly Label message = new() { AutoSize = true, MaximumSize = new Size(1100, 0), ForeColor = Color.DimGray };
    private readonly Panel properties = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly PictureBox paper = new() { Width = 460, Height = 300, BackColor = Color.White, SizeMode = PictureBoxSizeMode.StretchImage };
    private readonly CheckBox drawing = new() { Text = "使用绘图示例", AutoSize = true };
    private readonly TextBox sample = new() { Text = "Hello Paperang!\r\n这是一条用于设计版式的示例纸条。", Multiline = true, Height = 55, Dock = DockStyle.Top, MaxLength = 10000 };
    private readonly System.Windows.Forms.Timer debounce = new() { Interval = 180 };
    private readonly NoteRenderer renderer = new();
    private List<NoteRenderer.RenderedBlock> rendered = [];
    private bool loading;
    private Point? dragStart;
    private int dragIndex = -1;
    private RelayTemplate Current => Templates[Math.Max(0, templateList.SelectedIndex)];
    private int Index => blocks.SelectedIndex;

    public TemplateEditor(List<RelayTemplate> values, string selectedId)
    {
        Templates = JsonSerializer.Deserialize<List<RelayTemplate>>(JsonSerializer.Serialize(values, RelayClient.Json), RelayClient.Json)!;
        Text = "Paperang Studio · 图形化打印模板编辑器"; ClientSize = new Size(1160, 760); MinimumSize = new Size(1020, 670);
        AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Microsoft YaHei UI", 9); StartPosition = FormStartPosition.CenterParent;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(16) };
        root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.AutoSize));
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        var preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
        preset.Items.AddRange(["便签", "小票", "复古电传", "访客纪念"]); preset.SelectedIndex = 0;
        top.Controls.AddRange([new Label { Text = "模板", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, templateList, name,
            Button("复制", Copy), preset, Button("载入预设", () => AddTemplate(RelayTemplate.Preset(preset.Text))),
            Button("导入 JSON", Import), Button("导出 JSON", Export), Button("删除模板", Delete)]);
        root.Controls.Add(top);
        var sampleRow = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, AutoSize = true };
        sampleRow.ColumnStyles.Add(new(SizeType.Percent, 100)); sampleRow.ColumnStyles.Add(new(SizeType.AutoSize));
        sampleRow.Controls.Add(sample); sampleRow.Controls.Add(drawing); root.Controls.Add(sampleRow);
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        columns.ColumnStyles.Add(new(SizeType.Absolute, 210)); columns.ColumnStyles.Add(new(SizeType.Absolute, 280)); columns.ColumnStyles.Add(new(SizeType.Percent, 100));
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(0, 8, 10, 0) };
        left.RowStyles.Add(new(SizeType.AutoSize)); left.RowStyles.Add(new(SizeType.Percent, 100)); left.RowStyles.Add(new(SizeType.AutoSize));
        left.Controls.Add(new Label { Text = "区块（拖动排序）", AutoSize = true }); left.Controls.Add(blocks);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var addKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 }; addKind.Items.AddRange(RelayBlock.Kinds); addKind.SelectedIndex = 1;
        actions.Controls.AddRange([addKind, Button("添加", () => {
            if (Current.Blocks.Count >= 24) { message.Text = "每个模板最多 24 个区块"; return; }
            Current.Blocks.Add(new RelayBlock { Kind = addKind.Text, Text = addKind.Text == "标题" ? "My note" : "" }); ReloadBlocks(Current.Blocks.Count - 1);
        }), Button("删除", () => { if (Index >= 0) { int i = Index; Current.Blocks.RemoveAt(i); ReloadBlocks(Math.Min(i, Current.Blocks.Count - 1)); } }),
            Button("上移", () => MoveBlock(-1)), Button("下移", () => MoveBlock(1))]);
        left.Controls.Add(actions); columns.Controls.Add(left);
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(8) };
        kind.Items.AddRange(RelayBlock.Kinds); font.Items.AddRange(["PingFang SC", "Menlo", "Helvetica", "Times New Roman", "Microsoft YaHei", "Consolas", "Arial"]); alignment.Items.AddRange(["居左", "居中", "居右"]);
        Field(fields, "区块属性", enabled); Field(fields, "内容", kind); Field(fields, "文字 / 变量", text);
        Field(fields, "", new Label { Text = "{{text}} {{sender}} {{id}} {{time}}\n{{ip}} {{ua}} {{device}}\n正文区块同时接收图片与说明文字。", AutoSize = true, ForeColor = Color.DimGray });
        Field(fields, "字体", font); Field(fields, "字号（8–96）", size); Field(fields, "", bold); Field(fields, "对齐", alignment);
        Field(fields, "左右边距（点）", margin); Field(fields, "上方留白（行）", gap);
        Field(fields, "", new Label { Text = "Mac 字体映射：\nPingFang SC → 微软雅黑\nMenlo → Consolas\nHelvetica → Arial\n模板 JSON 保留原字体名。", AutoSize = true, ForeColor = Color.DimGray });
        properties.Controls.Add(fields); columns.Controls.Add(properties);
        var preview = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(226, 229, 233), Padding = new Padding(16) };
        paper.Location = new Point(16, 16); preview.Controls.Add(paper); columns.Controls.Add(preview); root.Controls.Add(columns);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        bottom.Controls.AddRange([Button("保存并应用", Save), Button("取消", () => Close()), message]); root.Controls.Add(bottom); Controls.Add(root);

        templateList.SelectedIndexChanged += (_, _) => { if (!loading) { loading = true; name.Text = Current.Name; loading = false; ReloadBlocks(0); } };
        name.TextChanged += (_, _) => { if (!loading) { int index = templateList.SelectedIndex; Templates[index] = Current with { Name = name.Text }; RefreshTemplateList(index); } };
        blocks.SelectedIndexChanged += (_, _) => LoadBlock();
        foreach (var control in new Control[] { enabled, kind, text, font, size, bold, alignment, margin, gap })
        {
            switch (control)
            {
                case CheckBox c: c.CheckedChanged += (_, _) => Edit(); break;
                case ComboBox c: c.TextChanged += (_, _) => Edit(); break;
                case NumericUpDown n: n.ValueChanged += (_, _) => Edit(); break;
                case TextBox t: t.TextChanged += (_, _) => Edit(); break;
            }
        }
        sample.TextChanged += (_, _) => Schedule(); drawing.CheckedChanged += (_, _) => Schedule();
        debounce.Tick += (_, _) => { debounce.Stop(); Render(); };
        blocks.MouseDown += (_, e) => dragIndex = blocks.IndexFromPoint(e.Location);
        blocks.MouseMove += (_, e) => { if (e.Button == MouseButtons.Left && dragIndex >= 0) { int i = dragIndex; dragIndex = -1; blocks.DoDragDrop(i, DragDropEffects.Move); } };
        blocks.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(typeof(int)) == true ? DragDropEffects.Move : DragDropEffects.None;
        blocks.DragDrop += (_, e) => {
            if (e.Data?.GetData(typeof(int)) is not int from) return;
            int to = blocks.IndexFromPoint(blocks.PointToClient(new Point(e.X, e.Y))); if (to < 0) to = Current.Blocks.Count - 1;
            if (from < 0 || from >= Current.Blocks.Count || to < 0) return;
            var block = Current.Blocks[from]; Current.Blocks.RemoveAt(from); Current.Blocks.Insert(to, block); ReloadBlocks(to);
        };
        paper.MouseDown += (_, e) => {
            int y = (int)(e.Y * 576.0 / paper.Width), top = 0;
            foreach (var part in rendered)
            {
                top += part.Gap;
                if (y >= top && y < top + part.Raster.Height) { blocks.SelectedIndex = Current.Blocks.FindIndex(x => x.Id == part.Id); dragStart = e.Location; return; }
                top += part.Raster.Height;
            }
        };
        paper.MouseUp += (_, e) => {
            if (dragStart is not Point start || Index < 0) return; dragStart = null;
            int dx = (int)((e.X - start.X) * 576.0 / paper.Width), dy = (int)((e.Y - start.Y) * 576.0 / paper.Width);
            if (Math.Abs(dx) + Math.Abs(dy) < 5) return;
            var block = Current.Blocks[Index]; Current.Blocks[Index] = block with { Margin = Math.Clamp(block.Margin + dx, 0, 120), Gap = Math.Clamp(block.Gap + dy, 0, 200) }; LoadBlock(); Schedule();
        };
        paper.Paint += (_, e) => {
            if (Index < 0 || paper.Image == null) return;
            int y = 0;
            foreach (var part in rendered)
            {
                y += part.Gap;
                if (part.Id == Current.Blocks[Index].Id) { using var pen = new Pen(Color.DodgerBlue, 2); float scale = paper.Width / 576f; e.Graphics.DrawRectangle(pen, 1, y * scale, paper.Width - 3, part.Raster.Height * scale); }
                y += part.Raster.Height;
            }
        };
        FormClosed += (_, _) => { debounce.Dispose(); paper.Image?.Dispose(); };
        int selected = Templates.FindIndex(x => x.Id == selectedId); RefreshTemplateList(Math.Max(0, selected)); ReloadBlocks(0);
    }
    private void RefreshTemplateList(int selected)
    {
        loading = true; templateList.Items.Clear(); templateList.Items.AddRange(Templates.Cast<object>().ToArray()); templateList.SelectedIndex = selected; name.Text = Current.Name; loading = false;
    }
    private void ReloadBlocks(int selected)
    {
        loading = true; blocks.Items.Clear(); blocks.Items.AddRange(Current.Blocks.Cast<object>().ToArray());
        if (Current.Blocks.Count > 0) blocks.SelectedIndex = Math.Clamp(selected, 0, Current.Blocks.Count - 1);
        loading = false; LoadBlock(); Schedule();
    }
    private void LoadBlock()
    {
        if (loading) return;
        properties.Enabled = Index >= 0;
        if (Index < 0) { paper.Invalidate(); return; }
        loading = true; var b = Current.Blocks[Index]; enabled.Checked = b.Enabled; kind.SelectedItem = b.Kind; text.Text = b.Text;
        text.Enabled = b.Kind is "标题" or "自定义文字"; font.Text = b.Font; size.Value = (decimal)b.Size; bold.Checked = b.Bold;
        alignment.SelectedItem = b.Alignment; margin.Value = b.Margin; gap.Value = b.Gap; loading = false; paper.Invalidate();
    }
    private void Edit()
    {
        if (loading || Index < 0) return;
        int i = Index; Current.Blocks[i] = Current.Blocks[i] with { Enabled = enabled.Checked, Kind = kind.Text, Text = text.Text,
            Font = font.Text, Size = (double)size.Value, Bold = bold.Checked, Alignment = alignment.Text, Margin = (int)margin.Value, Gap = (int)gap.Value };
        text.Enabled = kind.Text is "标题" or "自定义文字";
        loading = true; blocks.Items[i] = Current.Blocks[i]; blocks.SelectedIndex = i; loading = false; Schedule();
    }
    private void Schedule() { debounce.Stop(); debounce.Start(); }
    private void Render()
    {
        try
        {
            var job = SampleJob(sample.Text, drawing.Checked);
            var parts = renderer.Parts(job, Current);
            var raster = Raster.Stack(parts.Select(x => (x.Raster, x.Gap)));
            var image = NoteRenderer.Preview(raster); var old = paper.Image; paper.Image = image; old?.Dispose();
            rendered = parts; paper.Height = (int)Math.Ceiling(raster.Height * paper.Width / 576.0);
            message.Text = $"576 × {raster.Height} 点 · 点击选区块，横向拖动调边距，纵向拖动调留白"; paper.Invalidate();
        }
        catch (Exception e) { rendered = []; var old = paper.Image; paper.Image = null; old?.Dispose(); message.Text = "预览失败：" + e.Message; }
    }
    private void MoveBlock(int delta)
    {
        int i = Index; if (i < 0 || i + delta < 0 || i + delta >= Current.Blocks.Count) return;
        (Current.Blocks[i], Current.Blocks[i + delta]) = (Current.Blocks[i + delta], Current.Blocks[i]); ReloadBlocks(i + delta);
    }
    private void AddTemplate(RelayTemplate template)
    {
        if (Templates.Count >= 30) { message.Text = "最多 30 个模板"; return; }
        Templates.Add(template); RefreshTemplateList(Templates.Count - 1); ReloadBlocks(0);
    }
    private void Copy() => AddTemplate(Current with { Id = Guid.NewGuid().ToString(), Name = (Current.Name.Length > 75 ? Current.Name[..75] : Current.Name) + " 副本", Blocks = Current.Blocks.ToList() });
    private void Delete()
    {
        if (Templates.Count <= 1) { message.Text = "至少保留一个模板"; return; }
        int i = templateList.SelectedIndex; Templates.RemoveAt(i); RefreshTemplateList(Math.Min(i, Templates.Count - 1)); ReloadBlocks(0);
    }
    private void Import()
    {
        using var dialog = new OpenFileDialog { Filter = "模板 JSON|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 65536) throw new InvalidDataException("模板文件超过 64 KiB");
            var template = JsonSerializer.Deserialize<RelayTemplate>(File.ReadAllText(dialog.FileName), RelayClient.Json) ?? throw new InvalidDataException("模板为空");
            template.Validate(); AddTemplate(template with { Id = Guid.NewGuid().ToString() });
        }
        catch (Exception e) { message.Text = "导入失败：" + e.Message; }
    }
    private void Export()
    {
        try
        {
            Current.Validate();
            using var dialog = new SaveFileDialog { Filter = "模板 JSON|*.json", FileName = "Paperang-template.json" };
            if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(Current, RelayClient.Json));
        }
        catch (Exception e) { message.Text = "导出失败：" + e.Message; }
    }
    private void Save()
    {
        try { foreach (var t in Templates) t.Validate(); _ = Raster.Stack(renderer.Parts(SampleJob(sample.Text, drawing.Checked), Current).Select(x => (x.Raster, x.Gap))); DialogResult = DialogResult.OK; Close(); }
        catch (Exception e) { message.Text = "无法保存：" + e.Message; }
    }
    internal static RelayJob SampleJob(string text, bool image)
    {
        JsonElement? content = null;
        if (image)
        {
            byte[] data = new byte[72 * 120];
            for (int y = 10; y < 110; y++) for (int x = 30; x < 546; x++)
                if (y is < 15 or > 104 || x is < 35 or > 540 || Math.Abs(x - 288) + Math.Abs((y - 60) * 3) < 100) data[y * 72 + x / 8] |= (byte)(1 << (7 - x % 8));
            content = JsonSerializer.SerializeToElement(new { type = "raster1", schemaVersion = 1, width = 576, height = 120, bitOrder = "msb-first", blackIsOne = true, encoding = "base64", data = Convert.ToBase64String(data), sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data)) });
        }
        return new() { Id = new string('a', 40), ShortID = "A1B2C3D4E5F6", Text = text, Created = 1793740800, Content = content,
            Metadata = new() { ["identity"] = JsonSerializer.SerializeToElement("Sample visitor"), ["ip"] = JsonSerializer.SerializeToElement("192.0.2.1"), ["userAgent"] = JsonSerializer.SerializeToElement("Example browser"), ["browser"] = JsonSerializer.SerializeToElement(new { platform = "Sample device" }) } };
    }
    private static Button Button(string text, Action action) { var button = new Button { Text = text, AutoSize = true }; button.Click += (_, _) => action(); return button; }
    private static NumericUpDown Number(int min, int max) => new() { Minimum = min, Maximum = max, Width = 100 };
    private static void Field(TableLayoutPanel table, string label, Control control)
    {
        if (label.Length > 0) table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 9, 0, 4) });
        control.Dock = DockStyle.Top; table.Controls.Add(control);
    }
}
