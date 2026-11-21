using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Paperang.Core;
using Paperang.Windows;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	internal async Task CheckWorkspaceAsync()
	{
		suppressAutomaticPreview = true;
		await CheckSessionAsync();
		await CheckLocalApi();
		await CheckTerminal();
		var navigation = (NavigationView)Content;
		if (!document.IsLoaded || document.ActualWidth < 300 || document.ActualHeight < 200 ||
		    inspector.ActualWidth < 200 || inspector.ActualHeight < 100)
			throw new InvalidOperationException($"Workspace not laid out: {document.ActualWidth}x{document.ActualHeight}, inspector {inspector.ActualWidth}x{inspector.ActualHeight}");
		var workspaceItem = navigation.MenuItems[0];
		for (int cycle = 0; cycle < 3; cycle++)
			foreach (var item in navigation.MenuItems)
				navigation.SelectedItem = item;
		navigation.SelectedItem = workspaceItem;
		text.Text = "Windows 打印工作台\n中文与 English · 576 px";
		var rendered = NoteRenderer.RenderText(text.Text, "Microsoft YaHei", 30, false, "居左", 0, 0, 4);
		if (!rendered.Bytes.Any(value => value != 0))
			throw new InvalidOperationException("Text rendered blank");
		await Preview(rendered);
		var qr = WorkspaceFiles.Qr("https://example.com", 240, "M");
		if (!qr.Bytes.Any(value => value != 0))
			throw new InvalidOperationException("QR rendered blank");
		await Preview(qr);
		layers.Add(new(rendered, 0, 0, 576, rendered.Height));
		layers.Add(new(qr, 0, rendered.Height, 576, qr.Height));
		await ShowLayout();
		await ConfirmLayout();
		if (LayoutSnapshot()?.Layers.Count != 2)
			throw new InvalidOperationException("Layout did not preserve its objects");
		string directory = Path.Combine(Path.GetTempPath(), "Paperang-workspace-check", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		await CheckAddedFeatures(directory);
		await CheckRemoteQueue();
		await CheckWorkspaceFlows(directory);
		foreach (string extension in new[] { ".raw", ".json", ".png" })
		{
			string path = Path.Combine(directory, "preview" + extension);
			WorkspaceFiles.Export(path, RequirePreview());
			var restored = WorkspaceFiles.Open(path, 128);
			if (!restored.Bytes.AsSpan().SequenceEqual(RequirePreview().Bytes))
				throw new InvalidOperationException("Raster export round-trip failed: " + extension);
		}
		sourceMode = "text";
		text.Text = "今天的计划\n\n准备内容，调整设置，然后打印。\n\n支持文字、图片、二维码与 JSON。";
		selectEditorGroup?.Invoke("文字");
		await RenderActiveAsync(default);
		await Task.Delay(300);
		if (Environment.GetEnvironmentVariable("PAPERANG_WORKSPACE_SNAPSHOT") is string snapshot)
		{
			var capture = new RenderTargetBitmap();
			await capture.RenderAsync((FrameworkElement)Content);
			var pixels = (await capture.GetPixelsAsync()).ToArray();
			await using var file = File.Create(snapshot);
			using var stream = file.AsRandomAccessStream();
			var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
			encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
				(uint)capture.PixelWidth, (uint)capture.PixelHeight, 96, 96, pixels);
			await encoder.FlushAsync();
		}
	}

	private static async Task CheckSessionAsync()
	{
		var storage = new LocalStore(Path.Combine(Path.GetTempPath(), "Paperang-session-check", Guid.NewGuid().ToString("N")));
		var transport = new SessionPrinter();
		using var session = new ClientSession(transport, storage);
		session.SaveSettings(new());
		try
		{
			await session.UseLocalPrinterAsync(new Raster(1, new byte[72]), new(), default);
			throw new Exception("Print was allowed while paper permission was off");
		}
		catch (InvalidOperationException) { }
		session.SetPaperAllowed(true);
		await session.UseLocalPrinterAsync(null, new(), default);
		await session.UseLocalPrinterAsync(null, new(), default);
		if (transport.Connections != 1 || !session.Connected)
			throw new InvalidOperationException("A connected session was unnecessarily reconnected");
		using var cancellation = new CancellationTokenSource();
		var sending = session.UseLocalPrinterAsync(new Raster(1, new byte[72]), new(), cancellation.Token);
		await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
		try
		{
			await session.UseLocalPrinterAsync(null, new(), default);
			throw new Exception("Concurrent printer ownership was accepted");
		}
		catch (InvalidOperationException) { }
		try
		{
			session.SaveSettings(new() { Feed = 23 });
			throw new Exception("Settings changed during an active print");
		}
		catch (InvalidOperationException) { }
		cancellation.Cancel();
		try { await sending; throw new Exception("Cancellation was ignored"); }
		catch (OperationCanceledException) { }
		if (session.Connected || session.Settings().Feed != P2Defaults.FinalFeed)
			throw new InvalidOperationException("Failed send did not release the connection or settings changed");
		await session.UseLocalPrinterAsync(null, new(), default);
		if (!session.Connected || transport.Connections != 2)
			throw new InvalidOperationException("The printer could not reconnect after cancellation");
	}

	private sealed class SessionPrinter : IPrinter
	{
		public int Connections;
		public bool Ready { get; private set; }
		public string Status => Ready ? "测试打印机已连接" : "未连接";
		public double Progress => 0;
		public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public Task ConnectAsync(AppSettings settings, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Connections++;
			Ready = true;
			return Task.CompletedTask;
		}
		public async Task PrintAsync(Raster raster, AppSettings settings, CancellationToken token)
		{
			Started.TrySetResult();
			await Task.Delay(Timeout.Infinite, token);
		}
		public Task RefreshAsync(CancellationToken token) => Task.CompletedTask;
		public void Disconnect() => Ready = false;
		public void Dispose() => Disconnect();
	}
}
