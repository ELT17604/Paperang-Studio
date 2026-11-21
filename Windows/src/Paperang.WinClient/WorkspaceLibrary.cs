using Paperang.Core;
using Paperang.Windows;

namespace Paperang.WinClient;

internal sealed record TextDraft(string Text, string Font, int Size, bool Bold, string Alignment, int Margin, int Spacing, int MarginY = 0);
internal sealed record LayoutDraft(List<PaperLayer> Layers);

internal sealed record WorkspaceEntry(Guid Id, DateTimeOffset Created, string Name, Raster Raster,
                                      LocalPrintOptions Options, TextDraft? Draft, bool Material, LayoutDraft? Layout = null)
{
	public override string ToString() => $"{(Material ? "素材" : "打印")} · {Name} · {Created.LocalDateTime:g}";
}

internal static class WorkspaceLibrary
{
	private static readonly LocalStore Store = new(Path.Combine(LocalStore.Root, "WinClient"));

	internal static List<WorkspaceEntry> Read() => Store.LoadWorkspaceLibrary<List<WorkspaceEntry>>() ?? [];

	internal static void Add(string name, Raster raster, LocalPrintOptions options, TextDraft? draft, bool material,
	                         LayoutDraft? layout = null)
	{
		var current = Read();
		current.Insert(0, new(Guid.NewGuid(), DateTimeOffset.UtcNow, name, raster.Validate(), options, draft, material, layout));
		var retained = new List<WorkspaceEntry>();
		int remaining = 2_650_000;
		bool added = false;
		foreach (var entry in current)
		{
			int size = entry.Raster.Bytes.Length + (entry.Layout?.Layers.Sum(layer => layer.Image.Bytes.Length) ?? 0) +
			           (entry.Layout?.Layers.Sum(layer => layer.Source.HasValue ?
				            System.Text.Encoding.UTF8.GetByteCount(layer.Source.Value.GetRawText()) : 0) ?? 0) +
			           (entry.Draft?.Text.Length ?? 0) * 6 + 1024;
			if (retained.Count == 0 && size > remaining)
				throw new InvalidDataException("此排版超过本地素材容量，请先导出文件。");
			if (size > remaining || retained.Count(item => item.Material == entry.Material) >= 10)
				continue;
			retained.Add(entry);
			added |= entry.Id == current[0].Id;
			remaining -= size;
		}
		if (!added) throw new InvalidDataException("素材未能保存，原列表未改变。");
		Store.SaveWorkspaceLibrary(retained);
	}

	internal static void Remove(Guid id) => Store.SaveWorkspaceLibrary(Read().Where(item => item.Id != id).ToList());
}
