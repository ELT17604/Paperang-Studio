using Paperang.Core;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace Paperang.WinLite;

public sealed record PrinterChoice(ulong Address, string Name, short Rssi)
{
	public override string ToString() => $"{Name} · {Address:X12} · {Rssi} dBm";
}

public sealed class BlePrinter : IPrinter
{
	private readonly Action<string>? trace;
	private string lastStatus = "蓝牙：未连接";
	public BlePrinter(Action<string>? trace = null) => this.trace = trace;
	private WindowsBleLink? link;
	private P2Session? session;
	public bool Ready => link?.Connected == true && session?.Ready == true;
	public string Status => Ready ? session!.Status : lastStatus;
	public double Progress => session?.Progress ?? 0;
	public string PartitionDetail => session?.PartitionDetail ?? "";
	public static async Task<PrinterChoice[]> ScanAsync(bool showAll, CancellationToken ct)
	{
		var devices = new Dictionary<ulong, PrinterChoice>();
		var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
		BluetoothError? stopped = null;
		watcher.Stopped += (_, args) => stopped = args.Error;
		watcher.Received += (_, args) =>
		{
			string name = args.Advertisement.LocalName;
			if (string.IsNullOrWhiteSpace(name))
				return;
			bool candidate = new[] { "paperang", "miaomiaoji", "喵喵机" }.Any(
			    x => name.Contains(x, StringComparison.OrdinalIgnoreCase));
			if (showAll || candidate)
				lock (devices) devices[args.BluetoothAddress] =
				    new(args.BluetoothAddress, name, args.RawSignalStrengthInDBm);
		};
		watcher.Start();
		try
		{
			await Task.Delay(TimeSpan.FromSeconds(12), ct);
		}
		finally
		{
			watcher.Stop();
		}
		if (stopped is not null and not BluetoothError.Success)
			throw new InvalidOperationException("蓝牙扫描不可用，请检查适配器与 Windows 蓝牙权限");
		lock (devices) return devices.Values.OrderByDescending(x => x.Rssi).ToArray();
	}
	public async Task ConnectAsync(AppSettings settings, CancellationToken ct)
	{
		Disconnect();
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(TimeSpan.FromSeconds(25));
		try
		{
			lastStatus = "正在连接 P2";
			link = await WindowsBleLink.OpenAsync(Convert.ToUInt64(settings.PrinterAddress, 16), timeout.Token, trace);
			// Subscribe session before CCCD enables the initial FF03 credits.
			session = new P2Session(link);
			await link.SubscribeAsync(timeout.Token);
			await session.HandshakeAsync(timeout.Token);
		}
		catch (Exception ex)
		{
			Disconnect();
			lastStatus = "蓝牙连接失败：" + ex.Message;
			trace?.Invoke(lastStatus);
			throw;
		}
	}
	public Task PrintAsync(Raster raster, AppSettings settings,
	                       CancellationToken ct) => session?.PrintAsync(raster, settings, ct) ??
	                                                throw new InvalidOperationException("打印机未连接");
	public Task RefreshAsync(CancellationToken ct) => session?.RefreshAsync(ct) ?? Task.CompletedTask;
	public void Disconnect()
	{
		session?.Dispose();
		session = null;
		link?.Dispose();
		link = null;
		lastStatus = "蓝牙：未连接";
	}
	public void Dispose() => Disconnect();
}

internal sealed class WindowsBleLink : IBleLink
{
	private Action<string>? trace;
	private BluetoothLEDevice? device;
	private GattDeviceService? service;
	private GattCharacteristic? write, data, flow;
	private GattSession? gattSession;
	public event Action<byte[]>? Data;
	public event Action<byte[]>? Flow;
	private bool disposed;
	public bool Connected => !disposed && device?.ConnectionStatus == BluetoothConnectionStatus.Connected;
	public int MaxWrite => Math.Max(1, Math.Min(180, (gattSession?.MaxPduSize ?? 23) - 3));
	private static Guid Uuid(ushort id) => Guid.Parse($"0000{id:x4}-0000-1000-8000-00805f9b34fb");
	public static async Task<WindowsBleLink> OpenAsync(ulong address, CancellationToken ct,
	                                                   Action<string>? trace = null)
	{
		var result = new WindowsBleLink { trace = trace };
		try
		{
			trace?.Invoke("Creating BLE device");
			result.device = await BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(ct);
			if (result.device == null)
				throw new InvalidOperationException("Windows 无法访问该蓝牙设备");
			// This P2/Windows stack returns Success + zero entries for UUID-filtered
			// discovery even though full uncached discovery returns FF00.
			var services = await result.device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(ct);
			trace?.Invoke(
			    $"Service discovery: {services.Status}, count={services.Services.Count}, protocol={services.ProtocolError}");
			result.service = services.Services.FirstOrDefault(x => x.Uuid == Uuid(0xff00));
			foreach (var extra in services.Services.Where(x => !ReferenceEquals(x, result.service)))
				extra.Dispose();
			if (services.Status != GattCommunicationStatus.Success || result.service == null)
				throw new InvalidOperationException($"未找到 P2 FF00 服务（{services.Status}）");
			result.gattSession = await GattSession.FromDeviceIdAsync(result.device.BluetoothDeviceId).AsTask(ct);
			if (result.gattSession != null)
				result.gattSession.MaintainConnection = true;
			var chars = await result.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(ct);
			trace?.Invoke($"Characteristics: {chars.Status}");
			foreach (var c in chars.Characteristics)
				trace?.Invoke($"  {c.Uuid}: {c.CharacteristicProperties}");
			if (chars.Status != GattCommunicationStatus.Success)
				throw new InvalidOperationException("无法读取 GATT 特征");
			result.write = chars.Characteristics.FirstOrDefault(x => x.Uuid == Uuid(0xff02));
			result.data = chars.Characteristics.FirstOrDefault(x => x.Uuid == Uuid(0xff01));
			result.flow = chars.Characteristics.FirstOrDefault(x => x.Uuid == Uuid(0xff03));
			if (result.write == null || result.data == null || result.flow == null ||
			    !result.write.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
				throw new InvalidOperationException("P2 特征或无响应写入能力缺失");
			return result;
		}
		catch
		{
			result.Dispose();
			throw;
		}
	}
	public async Task SubscribeAsync(CancellationToken ct)
	{
		data!.ValueChanged += OnData;
		flow!.ValueChanged += OnFlow;
		foreach (var characteristic in new[] { data, flow })
		{
			var status = await characteristic
			                 .WriteClientCharacteristicConfigurationDescriptorAsync(
			                     GattClientCharacteristicConfigurationDescriptorValue.Notify)
			                 .AsTask(ct);
			trace?.Invoke($"Subscribe {characteristic.Uuid}: {status}");
			if (status != GattCommunicationStatus.Success)
				throw new InvalidOperationException("无法订阅 P2 通知与流控");
		}
	}
	private void OnData(GattCharacteristic _, GattValueChangedEventArgs args)
	{
		var bytes = args.CharacteristicValue.ToArray();
		trace?.Invoke("RX DATA " + Convert.ToHexString(bytes));
		Data?.Invoke(bytes);
	}
	private void OnFlow(GattCharacteristic _, GattValueChangedEventArgs args)
	{
		var bytes = args.CharacteristicValue.ToArray();
		trace?.Invoke("RX FLOW " + Convert.ToHexString(bytes));
		Flow?.Invoke(bytes);
	}
	public async Task WriteAsync(byte[] bytes, CancellationToken ct)
	{
		trace?.Invoke($"TX BLE {bytes.Length} bytes");
		var result =
		    await write!.WriteValueWithResultAsync(bytes.AsBuffer(), GattWriteOption.WriteWithoutResponse).AsTask(ct);
		if (result.Status != GattCommunicationStatus.Success)
			throw new InvalidOperationException("Windows GATT 写入失败");
	}
	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		if (data != null)
			data.ValueChanged -= OnData;
		if (flow != null)
			flow.ValueChanged -= OnFlow;
		if (gattSession != null)
		{
			gattSession.MaintainConnection = false;
			gattSession.Dispose();
		}
		service?.Dispose();
		device?.Dispose();
	}
}
