using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Paperang.Core;

public sealed record AppSettings
{
    public string Server { get; init; } = "";
    public string AuthenticationMode { get; init; } = "owner";
    public string OwnerKeyFile { get; init; } = "";
    public string DeviceKey { get; init; } = "";
    public bool AllowHttp { get; init; }
    public string PrinterAddress { get; init; } = "";
    public string PrinterName { get; init; } = "";
    public string PrinterTransport { get; init; } = "auto";
    public bool AutoConnect { get; init; } = true;
    public bool AutoAllowPrint { get; init; } = true;
    public bool KeepReconnecting { get; init; } = true;
    public bool? StartAtLogin { get; init; }
    public string Style { get; init; } = "便签";
    public int Density { get; init; } = 100;
    public int Feed { get; init; } = 150;
    public bool IncludeSender { get; init; } = true;
    public bool IncludeTime { get; init; } = true;
    public bool IncludeId { get; init; }
    public List<RelayTemplate> Templates { get; init; } = RelayTemplate.Defaults();
    public string TemplateId { get; init; } = "";
    public AppSettings WithPrintingFrom(AppSettings source) => this with {
        Style = source.Style, Templates = source.Templates, TemplateId = source.TemplateId,
        Density = source.Density, Feed = source.Feed, IncludeSender = source.IncludeSender,
        IncludeTime = source.IncludeTime, IncludeId = source.IncludeId, AutoAllowPrint = source.AutoAllowPrint
    };
    public Uri Validate()
    {
        if (PrinterTransport is not ("auto" or "bluetooth")) throw new ArgumentException("不支持的打印机连接方式");
        if (!Uri.TryCreate(Server.Trim(), UriKind.Absolute, out var u) ||
            !string.IsNullOrEmpty(u.UserInfo) || !string.IsNullOrEmpty(u.Query) || !string.IsNullOrEmpty(u.Fragment) ||
            u.AbsolutePath != "/" || (u.Scheme != "https" && !(u.Scheme == "http" && (u.IsLoopback || AllowHttp))))
            throw new ArgumentException("请输入服务器根地址；公网 HTTP 需显式允许");
        if (AuthenticationMode == "owner")
        {
            if (string.IsNullOrWhiteSpace(OwnerKeyFile) || !File.Exists(OwnerKeyFile))
                throw new ArgumentException("请选择已完成服务器配对的所有者私钥文件；旧 M 密钥不能用于管理");
            OwnerAuthentication.Read(OwnerKeyFile, u);
        }
        else if (AuthenticationMode != "legacy") throw new ArgumentException("不支持的鉴权方式");
        else if (!(DeviceKey.StartsWith("psr_", StringComparison.Ordinal) || DeviceKey.StartsWith("pss_", StringComparison.Ordinal)) || DeviceKey.Length > 256 || DeviceKey.Any(char.IsWhiteSpace))
            throw new ArgumentException("请输入 psr_ 开头的设备密钥");
        if (Density is < 0 or > 100 || Feed is < 0 or > 2000 || !new[] { "便签", "小票", "复古电传", "访客纪念", "自定义" }.Contains(Style))
            throw new ArgumentException("打印设置超出范围");
        if (Templates == null || Templates.Count is < 1 or > 30 || Templates.Any(x => x == null || !Guid.TryParse(x.Id, out _)) || Templates.Select(x => x.Id.ToUpperInvariant()).Distinct().Count() != Templates.Count)
            throw new ArgumentException("模板数量需为 1–30 且编号唯一");
        foreach (var template in Templates) template.Validate();
        if (PrinterAddress.Length != 0 && (!ulong.TryParse(PrinterAddress, System.Globalization.NumberStyles.HexNumber, null, out ulong address) || address > 0xffffffffffff))
            throw new ArgumentException("打印机地址无效，请重新扫描选择");
        return u;
    }
}

public sealed record Raster(int Height, byte[] Bytes)
{
    public Raster Validate()
    {
        if (Height is < 1 or > 1400 || Bytes.Length != Height * 72) throw new ArgumentException("位图必须为 576 点宽、1–1400 行");
        return this;
    }
    public Raster Fitting(int margin)
    {
        Validate();
        if (margin is < 0 or > 120) throw new ArgumentOutOfRangeException(nameof(margin));
        if (margin == 0) return this;
        int width = 576 - 2 * margin, height = Math.Max(1, (Height * width + 575) / 576);
        var output = new Raster(height, new byte[72 * height]);
        for (int y = 0; y < height; y++)
        {
            int sy = Math.Min(Height - 1, ((2 * y + 1) * Height) / (2 * height));
            for (int x = 0; x < width; x++)
            {
                int sx = Math.Min(575, ((2 * x + 1) * 576) / (2 * width));
                if ((Bytes[sy * 72 + sx / 8] & (1 << (7 - sx % 8))) != 0)
                { int dx = x + margin; output.Bytes[y * 72 + dx / 8] |= (byte)(1 << (7 - dx % 8)); }
            }
        }
        return output;
    }
    public static Raster Stack(IEnumerable<(Raster Image, int Gap)> parts)
    {
        using var stream = new MemoryStream(); int height = 0, count = 0;
        foreach (var (image, gap) in parts)
        {
            image.Validate();
            if (++count > 24 || gap is < 0 or > 200 || (height += image.Height + gap) > 1400) throw new InvalidDataException("组合版式超过 1400 行，请缩小字号、留白或关闭部分区块");
            stream.Write(new byte[72 * gap]); stream.Write(image.Bytes);
        }
        if (count == 0) throw new InvalidDataException("请启用至少一个有内容的区块");
        return new(height, stream.ToArray());
    }
}

public sealed record RelayJob
{
    public string Id { get; init; } = "";
    public string ShortID { get; init; } = "";
    public string Text { get; init; } = "";
    public Dictionary<string, JsonElement> Metadata { get; init; } = [];
    public double Created { get; init; }
    public string State { get; init; } = "queued";
    public bool Truncated { get; init; }
    public JsonElement? Content { get; init; }
    [JsonIgnore] public string Sender => Metadata.TryGetValue("identity", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString()! : "Anonymous";
    public RelayJob Validate()
    {
        if (!Regex.IsMatch(Id, "^(?:[a-f0-9]{40}|[a-f0-9-]{36})$") || Text.Length > 10000 || Metadata is null || !double.IsFinite(Created))
            throw new InvalidDataException("服务器任务格式无效");
        return this;
    }
    public Raster? Image()
    {
        if (Content is null || Content.Value.ValueKind == JsonValueKind.Null) return null;
        var c = Content.Value;
        if (c.GetProperty("type").GetString() != "raster1" || c.GetProperty("schemaVersion").GetInt32() != 1 ||
            c.GetProperty("width").GetInt32() != 576 || c.GetProperty("bitOrder").GetString() != "msb-first" ||
            !c.GetProperty("blackIsOne").GetBoolean() || c.GetProperty("encoding").GetString() != "base64")
            throw new InvalidDataException("不支持的远程位图格式");
        string encoded = c.GetProperty("data").GetString() ?? "";
        if (encoded.Length > 134400) throw new InvalidDataException("位图过大");
        var bytes = Convert.FromBase64String(encoded);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != c.GetProperty("sha256").GetString()) throw new InvalidDataException("位图 SHA256 校验失败");
        return new Raster(c.GetProperty("height").GetInt32(), bytes).Validate();
    }
}

public sealed record PendingResult(string Server, string KeyFingerprint, string JobId, string Claim, string State = "unknown");
public interface IJournal { PendingResult? Load(); void Save(PendingResult? value); }
public interface IRasterizer { Raster Render(RelayJob job, AppSettings settings); }
public interface IPrinter : IDisposable
{
    string ActiveTransport => "";
    bool Ready { get; }
    string Status { get; }
    double Progress { get; }
    string PartitionDetail => "";
    Task ConnectAsync(AppSettings settings, CancellationToken ct);
    Task PrintAsync(Raster raster, AppSettings settings, CancellationToken ct);
    Task RefreshAsync(CancellationToken ct);
    void Disconnect();
}
