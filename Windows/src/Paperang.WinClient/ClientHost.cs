using Paperang.Core;
using Paperang.Windows;

namespace Paperang.WinClient;

internal sealed record ClientJob(Guid Id, DateTimeOffset Created, string Title, string State, string Detail)
{
	public override string ToString() => $"{Created.LocalDateTime:T} · {Title} · {State} · {Detail}";
}

internal sealed class ClientSession : IDisposable
{
	private readonly LocalStore store;
	private readonly IPrinter printer;
	private readonly SemaphoreSlim gate = new(1, 1);
	private readonly RelayEngine engine;
	private bool disposed;
	private CancellationTokenSource? activeOperation;
	private readonly object jobLock = new();
	private readonly List<ClientJob> jobs = [];
	private readonly List<ProtocolTrace> traces = [];
	internal ProtocolTrace[] Traces { get { lock (jobLock) return traces.ToArray(); } }
	internal void ClearTraces() { lock (jobLock) traces.Clear(); }
	private void RecordTrace(ProtocolTrace entry)
	{
		lock (jobLock)
		{
			traces.Add(entry);
			if (traces.Count > 500) traces.RemoveAt(0);
		}
	}
	internal ClientJob[] Jobs { get { lock (jobLock) return jobs.ToArray(); } }
	internal void CancelCurrent()
	{
		try { activeOperation?.Cancel(); }
		catch (ObjectDisposedException) { }
		if (engine.Running) engine.CancelForPaperPermission();
	}
	private void Record(ClientJob job)
	{
		lock (jobLock)
		{
			int index = jobs.FindIndex(item => item.Id == job.Id);
			if (index >= 0) jobs[index] = job;
			else jobs.Insert(0, job);
			if (jobs.Count > 100) jobs.RemoveAt(jobs.Count - 1);
		}
		Notify();
	}
	internal event Action? Changed;
	internal string DeviceStatus => printer.Status;
	internal string ActiveTransport => printer.ActiveTransport;
	internal bool Connected => printer.Ready;
	internal bool Working => gate.CurrentCount == 0 || engine.Busy;
	internal double Progress => engine.Busy ? engine.Progress : printer.Progress;
	internal bool AllowPaper { get; private set; }
	internal string RelayStatus => engine.Message;
	internal void SetPaperAllowed(bool value)
	{
		AllowPaper = value;
		if (!value)
		{
			CancelCurrent();
			engine.CancelForPaperPermission();
		}
		engine.SetAllowed(false);
		if (value) engine.SetAllowed(store.LoadSettings().AutoAllowPrint);
		Notify();
	}

	internal ClientSession(IPrinter? transport = null, LocalStore? settingsStore = null)
	{
		store = settingsStore ?? new();
		printer = transport ?? new PreferredPrinter(new UsbPrinter(), new BlePrinter());
		printer.ObserveTrace(RecordTrace);
		engine = new(printer, new NoteRenderer(), store, settings => new RelayClient(settings, productVersion: Program.Version), productVersion: Program.Version);
		engine.Changed += Notify;
	}
	private void Notify() => Changed?.Invoke();
	internal AppSettings Settings() => store.LoadForEditing();
	internal void SaveSettings(AppSettings value)
	{
		if (!gate.Wait(0))
			throw new InvalidOperationException("请等待当前操作结束后再保存连接。");
		try
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (engine.Running)
				throw new InvalidOperationException("请先断开远程打印再修改连接。");
			store.SaveSettings(value);
			store.SaveDraft(value);
			printer.Disconnect();
			Notify();
		}
		finally { gate.Release(); }
	}
	internal Task<string> RelayStatusAsync() => Task.FromResult(engine.Message + "\n" + engine.PrintMessage);
	internal void SavePrintingPreferences(AppSettings value)
	{
		if (!gate.Wait(0)) throw new InvalidOperationException("请等待当前本地操作结束。");
		try
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			var next = store.LoadSettings().WithPrintingFrom(value);
			if (next.Server.Length > 0) next.Validate();
			store.SaveSettings(next); store.SaveDraft(next);
			engine.ApplyPrintingPreferences(next);
			engine.SetAllowed(AllowPaper && next.AutoAllowPrint);
		}
		finally { gate.Release(); Notify(); }
	}
	internal void SaveStartupPreference(bool enabled)
	{
		if (!gate.Wait(0)) throw new InvalidOperationException("请等待当前操作结束。");
		try
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			StartupRegistration.Set(enabled);
			var saved = store.LoadSettings() with { StartAtLogin = enabled };
			store.SaveSettings(saved);
			store.SaveDraft(store.LoadForEditing() with { StartAtLogin = enabled });
		}
		finally { gate.Release(); Notify(); }
	}
	internal async Task RequestRemotePrintAsync(string id, CancellationToken token)
	{
		if (!await gate.WaitAsync(0, token)) throw new InvalidOperationException("当前有其他打印机操作。");
		try
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (!AllowPaper) throw new InvalidOperationException("请先打开全局允许耗纸开关。");
			engine.RequestPrint(id);
		}
		finally { gate.Release(); Notify(); }
	}
	internal async Task<string> SetRelayAsync(bool enabled)
	{
		await gate.WaitAsync();
		try
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (enabled)
			{
				printer.Disconnect();
				var settings = store.LoadSettings();
				await engine.StartAsync(settings with { AutoAllowPrint = AllowPaper && settings.AutoAllowPrint });
			}
			else
				await engine.StopAsync();
			return engine.Message;
		}
		finally { gate.Release(); Notify(); }
	}
	internal async Task<string> UseLocalPrinterAsync(Raster? raster, LocalPrintOptions options, CancellationToken token,
	                                               int manualFeed = 0, Guid? requestedId = null, int? densityOnly = null)
	{
		if (!await gate.WaitAsync(0, token))
			throw new InvalidOperationException("打印机正在处理另一项操作。");
		using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
		activeOperation = lifetime;
		token = lifetime.Token;
		ClientJob? job = null;
		bool sending = false;
		try
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if ((raster != null || manualFeed > 0) && !AllowPaper)
				throw new InvalidOperationException("请先打开左侧的全局允许耗纸开关。");
			if (engine.Running)
				throw new InvalidOperationException("请先在远程打印页面断开中转。");
			if (raster != null)
			{
				job = new(requestedId ?? Guid.NewGuid(), DateTimeOffset.UtcNow, $"{raster.Height} 行 × {options.Copies} 份", "连接中", "");
				Record(job);
			}
			var settings = store.LoadForEditing();
			if (options.TransferSettings != null) settings = settings with { TransferSettings = options.TransferSettings.Validate() };
			if (!printer.Ready)
				await printer.ConnectAsync(settings, token);
			printer.ConfigureTransfer(settings.TransferSettings ?? new());
			if (raster != null)
			{
				sending = true;
				Record(job! with { State = "发送中" });
				await options.SendAsync(printer, raster, settings, token);
				Record(job! with { State = "已发送", Detail = "请核对纸面" });
			}
			else if (manualFeed > 0)
				await printer.FeedAsync(manualFeed, token);
			else if (densityOnly.HasValue)
				await printer.ApplyDensityAsync(densityOnly.Value, token);
			else
				await printer.RefreshAsync(token);
			return printer.Status;
		}
		catch
		{
			if (job != null) Record(job with { State = sending ? "结果待核对" : "未发送",
				Detail = sending ? "已中止；请检查纸面后再决定是否重试" : "连接或准备失败" });
			printer.Disconnect();
			throw;
		}
		finally { activeOperation = null; gate.Release(); Notify(); }
	}
	internal async Task StopAsync() => await SetRelayAsync(false);
	internal async Task<byte[]> QueryAsync(byte command, CancellationToken token)
	{
		P2Queries.Expected(command);
		if (!await gate.WaitAsync(0, token)) throw new InvalidOperationException("打印机正忙。");
		using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
		activeOperation = lifetime;
		try
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (engine.Running) throw new InvalidOperationException("请先断开中转监听。");
			if (!printer.Ready) await printer.ConnectAsync(store.LoadForEditing(), lifetime.Token);
			return await printer.QueryAsync(command, lifetime.Token);
		}
		catch { printer.Disconnect(); throw; }
		finally { activeOperation = null; gate.Release(); Notify(); }
	}
	internal async Task DisconnectAsync()
	{
		if (!await gate.WaitAsync(0))
			throw new InvalidOperationException("请先取消当前操作，再断开打印机。");
		try
		{
			if (engine.Running) await engine.StopAsync();
			printer.Disconnect();
		}
		finally { gate.Release(); Notify(); }
	}
	internal async Task SendEngineeringAsync(byte command, byte[] payload, WorkspaceSettings transfer, CancellationToken token)
	{
		P2Engineering.Validate(command, payload);
		if (!await gate.WaitAsync(0, token)) throw new InvalidOperationException("打印机正忙。");
		using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
		activeOperation = lifetime;
		try
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (engine.Running) throw new InvalidOperationException("请先断开中转监听。");
			if (P2Engineering.UsesPaper(command) && !AllowPaper) throw new InvalidOperationException("该命令耗纸，请先打开全局允许耗纸。");
			if (!printer.Ready) await printer.ConnectAsync(store.LoadForEditing() with { TransferSettings = transfer }, lifetime.Token);
			printer.ConfigureTransfer(transfer);
			await printer.SendEngineeringAsync(command, payload, lifetime.Token);
		}
		catch { printer.Disconnect(); throw; }
		finally { activeOperation = null; gate.Release(); Notify(); }
	}
	public void Dispose()
	{
		disposed = true;
		engine.Changed -= Notify;
		printer.Dispose();
	}
}
