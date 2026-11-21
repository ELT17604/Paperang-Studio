using System.Text.Json;
using System.Text.Json.Serialization;
using Paperang.Core;

namespace Paperang.WinClient;

internal sealed record WorkspaceJsonImport(Raster? Raster = null, WorkspaceSettings? Settings = null);
internal static class WorkspaceJson
{
	internal static WorkspaceJsonImport Read(string path)
	{
		if (new FileInfo(path).Length is < 2 or > 144_000) throw new InvalidDataException("JSON 文件为空或超过大小上限。");
		using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 32 });
		var root = document.RootElement;
		if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("请选择点阵或参数预设 JSON。");
		var names = root.EnumerateObject().Select(field => field.Name).ToArray();
		if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) throw new InvalidDataException("JSON 含有重复字段。");
		if (root.TryGetProperty("type", out var type) && type.GetString() == "raster1")
			return new(Raster: WorkspaceFiles.Open(path, new WorkspaceSettings()));
		if (root.TryGetProperty("version", out _) && root.TryGetProperty("fontName", out _))
		{
			var options = new JsonSerializerOptions(RelayClient.Json) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
			var required = JsonSerializer.SerializeToElement(new WorkspaceSettings(), options).EnumerateObject().Select(field => field.Name);
			if (required.Any(name => !root.TryGetProperty(name, out _))) throw new InvalidDataException("参数预设字段不完整，请从 Mac 或 Android 客户端重新导出完整预设。");
			return new(Settings: (root.Deserialize<WorkspaceSettings>(options) ?? throw new InvalidDataException("预设为空。")).Validate());
		}
		throw new InvalidDataException("无法识别 JSON。支持 Mac / Android 的 raster1 点阵和 version 1 参数预设。");
	}
}
