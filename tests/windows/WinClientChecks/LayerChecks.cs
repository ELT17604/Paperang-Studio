using System.Text.Json;
using Microsoft.UI.Text;
using Paperang.Core;
using Paperang.Windows;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private void CheckLayerEditing()
	{
		layerText.Document.SetText(TextSetOptions.None, "Plain bold\n中文");
		var selection = layerText.Document.GetRange(6, 10);
		selection.CharacterFormat.Bold = FormatEffect.On;
		selection.CharacterFormat.Size = 20;
		layerText.Document.GetText(TextGetOptions.FormatRtf, out string rtf);
		var source = new LayerSource("rich", "Plain bold\n中文", rtf.TrimEnd('\0'), new WorkspaceSettings());
		var layer = RenderEditableLayer(new(new Raster(180, new byte[180 * 72]), 8, 4, 280, 180,
			Source: JsonSerializer.SerializeToElement(source, RelayClient.Json)));
		if (layer.SourceWidth != 280 || !layer.Image.Bytes.Any(value => value != 0))
			throw new InvalidOperationException("Rich text did not render with the object's native width");
		var frame = PaperLayout.Render(200, [layer]);
		string serialized = JsonSerializer.Serialize(new LayoutDraft([layer]), RelayClient.Json);
		var restored = JsonSerializer.Deserialize<LayoutDraft>(serialized, RelayClient.Json)!;
		if (ReadLayerSource(restored.Layers[0])?.Rtf != source.Rtf ||
		    !PaperLayout.Render(200, restored.Layers).Bytes.AsSpan().SequenceEqual(frame.Bytes))
			throw new InvalidOperationException("Editable text geometry or style did not survive draft restoration");
		var label = RenderEditableLayer(layer with { Source = JsonSerializer.SerializeToElement(source with { Kind = "label" }, RelayClient.Json) });
		if (label.Image.Bytes.AsSpan().SequenceEqual(layer.Image.Bytes))
			throw new InvalidOperationException("Label border was not rendered");
		var old = JsonSerializer.Deserialize<PaperLayer>(JsonSerializer.Serialize(new { image = layer.Image, x = 0, y = 0, width = 576, height = 180 }, RelayClient.Json), RelayClient.Json)!;
		if (old.SourceWidth != 576) throw new InvalidOperationException("Older raster-only materials no longer restore");
		Console.WriteLine("PASS rich text selection styles, native-width layout, label borders and editable/legacy draft restoration");
	}
}
