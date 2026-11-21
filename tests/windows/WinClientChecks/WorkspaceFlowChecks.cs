using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Paperang.Core;
using Paperang.Windows;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private async Task CheckWorkspaceFlows(string directory)
	{
		await Task.Delay(150);
		if (editingSurface!.ActualWidth <= previewSurface!.ActualWidth)
			throw new InvalidOperationException($"Editing does not occupy the primary work area: {editingSurface.ActualWidth} / {previewSurface.ActualWidth}");
		var navigation = (NavigationView)Content;
		var previous = navigation.SelectedItem;
		sourceMode = "text"; text.Text = "待加入排版的内容";
		await RenderActiveAsync(default);
		var content = RequirePreview();
		selectEditorGroup?.Invoke("高级排版 · 自由图层");
		if (sourceMode != "text" || !RequirePreview().Bytes.AsSpan().SequenceEqual(content.Bytes))
			throw new Exception("Opening layout tools discarded the content waiting to be added");
		selectEditorGroup?.Invoke("文字");
		var provider = scanProvider;
		var more = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Task? scanning = null;
		try
		{
			navigation.SelectedItem = navigation.MenuItems.Cast<NavigationViewItem>().Single(item => (string)item.Tag == "printer");
			await Task.Delay(100);
			scanProvider = async (_, token, updates) =>
			{
				updates.Report(new(1, "Paperang test A", -60));
				await more.Task.WaitAsync(token);
				updates.Report(new(2, "Paperang test B", -50));
				updates.Report(new(1, "Paperang test A", -44));
				await Task.Delay(Timeout.Infinite, token);
				return [];
			};
			scanning = StartPrinterScan();
			await Task.Delay(100);
			if (visiblePrinters.Count != 1 || !discoveredPrinters.IsEnabled || operation != null || !text.IsEnabled)
				throw new Exception("Scan blocked editing/selection or withheld incremental results");
			discoveredPrinters.SelectedIndex = 0;
			var selected = discoveredPrinters.SelectedItem;
			if (printerAddress.Text != "000000000001" || !connectPrinter.IsEnabled)
				throw new Exception("A discovered device could not be selected before scanning completed");
			more.SetResult(); await Task.Delay(100);
			if (visiblePrinters.Count != 2 || !ReferenceEquals(selected, discoveredPrinters.SelectedItem) || !visiblePrinters[0].Detail.Contains("-44"))
				throw new Exception("Live signal updates replaced or lost the selected device");
			discoveredPrinters.ScrollIntoView(selected); discoveredPrinters.UpdateLayout(); await Task.Delay(80);
			var container = discoveredPrinters.ContainerFromItem(selected) as Microsoft.UI.Xaml.DependencyObject;
			var labels = container == null ? [] : VisualLabels(container).ToArray();
			if (!labels.Contains("Paperang test A") || !labels.Any(label => label.Contains("-44")))
				throw new Exception("The real device row did not display the live name and signal bindings");
			scannerLifetime!.Cancel(); await scanning;
			if (!scanStart.IsEnabled || scanStop.IsEnabled || scannerLifetime != null) throw new Exception("Stopped scan retained its busy state");
		}
		finally
		{
			scannerLifetime?.Cancel(); if (scanning != null) await scanning;
			scanProvider = provider; knownPrinters.Clear(); visiblePrinters.Clear();
			navigation.SelectedItem = previous;
		}
		await Task.Delay(100);
		sourceMode = "text"; text.Text = ""; await Task.Delay(50);
		string presetPath = Path.Combine(directory, "android-preset.json");
		var android = new WorkspaceSettings { FontName = "sans-serif", FontSize = 36, Density = 82, Copies = 2, FeedAfter = 172 };
		await File.WriteAllTextAsync(presetPath, JsonSerializer.Serialize(android, RelayClient.Json));
		await ImportPath(presetPath, "android-preset.json", default);
		if ((string)fontFamily.SelectedItem != "Microsoft YaHei" || font.Value != 36 || feed.Value != 172 || copies.Value != 2)
			throw new Exception("Android JSON preset did not restore font aliases and print settings");
		await File.WriteAllTextAsync(presetPath, JsonSerializer.Serialize(android with { FontName = "PingFang SC" }, RelayClient.Json));
		await ImportPath(presetPath, "mac-preset.json", default);
		if ((string)fontFamily.SelectedItem != "Microsoft YaHei") throw new Exception("Mac font alias did not import");
		var input = new Raster(4, new byte[288]); input.Bytes[0] = 0x80;
		string rasterPath = Path.Combine(directory, "mobile-raster.json");
		WorkspaceFiles.Export(rasterPath, input);
		await ImportPath(rasterPath, "mobile-raster.json", default);
		if (sourceMode != "raster" || !RequirePreview().Bytes.AsSpan().SequenceEqual(input.Bytes)) throw new Exception("JSON raster import changed paper pixels");
		await File.WriteAllTextAsync(presetPath, "{\"version\":1,\"fontName\":\"sans-serif\"}");
		try { WorkspaceJson.Read(presetPath); throw new Exception("Incomplete preset accepted"); }
		catch (InvalidDataException) { }
		if (!RequirePreview().Bytes.AsSpan().SequenceEqual(input.Bytes)) throw new Exception("Rejected JSON changed the current document");
		ApplyWorkspaceSettings(new()); await RenderActiveAsync(default);
		Console.WriteLine("PASS live scan results, selection during scan, stable device identity, cancellation, edit-first workspace and Mac/Android JSON imports");
	}
	private static IEnumerable<string> VisualLabels(Microsoft.UI.Xaml.DependencyObject parent)
	{
		if (parent is TextBlock label) yield return label.Text;
		for (int index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
			foreach (string value in VisualLabels(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, index))) yield return value;
	}
}
