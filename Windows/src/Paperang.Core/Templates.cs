using System.Text.Json;
using System.Text.RegularExpressions;

namespace Paperang.Core;

// Property names and UUID strings deliberately match Swift Codable v1 templates.
public sealed record RelayBlock
{
    public static readonly string[] Kinds = ["标题", "正文", "投稿身份", "时间", "任务编号", "IP", "UA", "浏览器信息", "分隔线", "自定义文字"];
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Kind { get; init; } = "正文";
    public string Text { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public string Font { get; init; } = "PingFang SC";
    public double Size { get; init; } = 24;
    public bool Bold { get; init; }
    public string Alignment { get; init; } = "居左";
    public int Margin { get; init; } = 12;
    public int Gap { get; init; } = 8;
    public override string ToString() => (Enabled ? "☑ " : "☐ ") + Kind + "  ·  " + Size;
    public string Value(RelayJob job)
    {
        string Meta(string name) => job.Metadata.TryGetValue(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "unknown";
        string id = job.ShortID.Length > 0 ? job.ShortID : job.Id.Replace("-", "")[..12].ToUpperInvariant();
        if (id.Length == 12) id = $"{id[..4]}-{id[4..8]}-{id[8..]}";
        string browser = job.Metadata.TryGetValue("browser", out var b) && b.ValueKind == JsonValueKind.Object
            ? string.Join(" · ", b.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => x.Name + "=" + x.Value.ToString())) : "";
        var fields = new Dictionary<string, string> {
            ["text"] = job.Text, ["sender"] = job.Sender, ["id"] = id,
            ["time"] = DateTimeOffset.UnixEpoch.AddSeconds(job.Created).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["ip"] = Meta("ip"), ["ua"] = Meta("userAgent"), ["device"] = browser
        };
        return Kind switch {
            "正文" => fields["text"], "投稿身份" => "投稿身份：" + fields["sender"], "时间" => fields["time"],
            "任务编号" => "#" + id, "IP" => "IP: " + fields["ip"], "UA" => "UA: " + fields["ua"],
            "浏览器信息" => browser, "分隔线" => "────────────────────────",
            _ => Regex.Replace(Text, @"\{\{(text|sender|id|time|ip|ua|device)\}\}", match => fields[match.Groups[1].Value])
        };
    }
}

public sealed record RelayTemplate
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public int Version { get; init; } = 1;
    public string Name { get; init; } = "我的样式";
    public List<RelayBlock> Blocks { get; init; } = [];
    public override string ToString() => Name;
    public void Validate()
    {
        if (!Guid.TryParse(Id, out _) || Version != 1 || string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || Blocks == null || Blocks.Count is < 1 or > 24 ||
            Blocks.Any(x => x == null || !Guid.TryParse(x.Id, out _)) || Blocks.Select(x => x.Id.ToUpperInvariant()).Distinct().Count() != Blocks.Count || !Blocks.Any(x => x.Enabled))
            throw new InvalidDataException("模板需有名称、1–24 个区块，且编号唯一、至少启用一个区块");
        foreach (var b in Blocks)
            if (!Guid.TryParse(b.Id, out _) || !RelayBlock.Kinds.Contains(b.Kind) || b.Text == null || b.Text.Length > 2000 || b.Font == null || b.Font.Length > 120 ||
                !double.IsFinite(b.Size) || b.Size is < 8 or > 96 || b.Margin is < 0 or > 120 || b.Gap is < 0 or > 200 || !new[] { "居左", "居中", "居右" }.Contains(b.Alignment))
                throw new InvalidDataException("区块参数无效或超出 macOS v1 模板范围");
    }
    public static List<RelayTemplate> Defaults() => new[] { "便签", "小票", "复古电传", "访客纪念" }.Select(Preset).ToList();
    public static RelayTemplate Preset(string name)
    {
        RelayBlock B(string kind, string text = "", double size = 24) => new() { Kind = kind, Text = text, Size = size };
        var body = B("正文"); var senderBlock = B("投稿身份", size: 16); var time = B("时间", size: 14);
        List<RelayBlock> blocks;
        switch (name)
        {
            case "小票": body = body with { Font = "Menlo", Size = 19 }; senderBlock = senderBlock with { Font = "Menlo" };
                blocks = [B("标题", "PAPERANG INBOX", 25), B("分隔线", size: 16), body, B("分隔线", size: 16), senderBlock, time]; break;
            case "复古电传": body = body with { Font = "Menlo", Size = 15 }; senderBlock = senderBlock with { Font = "Menlo" }; time = time with { Font = "Menlo" };
                blocks = [B("标题", "[ INCOMING TRANSMISSION ]", 17), senderBlock, time, body, B("标题", "[ END ]", 15)]; break;
            case "访客纪念": body = body with { Size = 27, Alignment = "居中" }; senderBlock = senderBlock with { Alignment = "居中" }; time = time with { Alignment = "居中" };
                blocks = [B("标题", "A NOTE TO KEEP", 35) with { Bold = true, Alignment = "居中" }, body, senderBlock, time]; break;
            default: blocks = [body, senderBlock, time]; break;
        }
        blocks.Add(B("任务编号", size: 14) with { Enabled = false });
        return new() { Name = name, Blocks = blocks };
    }
}
