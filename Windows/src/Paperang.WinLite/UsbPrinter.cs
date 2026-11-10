using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Paperang.Core;

namespace Paperang.WinLite;

public sealed class UsbPrinter : IPrinter
{
    private WindowsUsbLink? link;
    private UsbP2Session? session;
    private string lastStatus = "USB：未连接";
    public string ActiveTransport => "usb";
    public bool Ready => link?.Connected == true && session?.Ready == true;
    public string Status => Ready ? session!.Status : lastStatus;
    public double Progress => session?.Progress ?? 0;
    public string PartitionDetail => session?.PartitionDetail ?? "";
    public async Task ConnectAsync(AppSettings settings, CancellationToken ct)
    {
        Disconnect();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            lastStatus = "正在连接 USB P2"; timeout.Token.ThrowIfCancellationRequested();
            link = WindowsUsbLink.Open(); session = new(link);
            await session.ConnectAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { Disconnect(); lastStatus = "USB 握手超时"; throw new InvalidOperationException(lastStatus); }
        catch (Exception e) { Disconnect(); lastStatus = "USB 连接失败：" + e.Message; throw; }
    }
    public Task PrintAsync(Raster raster, AppSettings settings, CancellationToken ct) => session?.PrintAsync(raster, settings, ct) ?? throw new InvalidOperationException("USB 打印机未连接");
    public Task RefreshAsync(CancellationToken ct) => session?.RefreshAsync(ct) ?? Task.CompletedTask;
    public void Disconnect() { session?.Dispose(); session = null; link?.Dispose(); link = null; lastStatus = "USB：未连接"; }
    public void Dispose() => Disconnect();
}

internal sealed class WindowsUsbLink : IUsbLink
{
    private static readonly Guid InterfaceGuid = new("28d78fad-5a12-11d1-ae5b-0000f803a8c2");
    private readonly FileStream stream;
    private bool connected = true;
    private WindowsUsbLink(SafeFileHandle handle) => stream = new FileStream(handle, FileAccess.ReadWrite, 4096, isAsync: true);
    public bool Connected => connected;
    internal static string[] DevicePaths()
    {
        var guid = InterfaceGuid;
        IntPtr info = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
        if (info == new IntPtr(-1)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var paths = new List<string>();
            for (uint i = 0; ; i++)
            {
                var item = new DeviceInterface { Size = Marshal.SizeOf<DeviceInterface>() };
                if (!SetupDiEnumDeviceInterfaces(info, IntPtr.Zero, ref guid, i, ref item))
                {
                    if (Marshal.GetLastWin32Error() == 259) break;
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                SetupDiGetDeviceInterfaceDetail(info, ref item, IntPtr.Zero, 0, out uint needed, IntPtr.Zero);
                if (Marshal.GetLastWin32Error() != 122) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                IntPtr detail = Marshal.AllocHGlobal(checked((int)needed));
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(info, ref item, detail, needed, out _, IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    string path = Marshal.PtrToStringUni(detail + 4) ?? "";
                    if (path.StartsWith(@"\\?\usb#vid_4348&pid_5584#", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
            return paths.ToArray();
        }
        finally { SetupDiDestroyDeviceInfoList(info); }
    }
    public static WindowsUsbLink Open()
    {
        var paths = DevicePaths();
        if (paths.Length != 1) throw new InvalidOperationException(paths.Length == 0 ? "未发现 USB P2，请检查电源与数据线" : "发现多台 USB P2，请只连接要使用的一台");
        var handle = CreateFile(paths[0], 0xc0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
        if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new System.ComponentModel.Win32Exception(error); }
        try { return new(handle); } catch { handle.Dispose(); throw; }
    }
    public async Task WriteAsync(byte[] data, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));
        try
        {
            await stream.WriteAsync(data, timeout.Token).ConfigureAwait(false);
            await stream.FlushAsync(timeout.Token).ConfigureAwait(false);
        }
        catch { connected = false; throw; }
    }
    public async Task<byte[]> ReadAsync(TimeSpan duration, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(duration);
        var buffer = new byte[4096];
        try { int n = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false); return buffer[..n]; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return []; }
        catch { connected = false; throw; }
    }
    public void Dispose() { connected = false; stream.Dispose(); }
    [StructLayout(LayoutKind.Sequential)] private struct DeviceInterface { public int Size; public Guid Guid; public uint Flags; public UIntPtr Reserved; }
    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr info, IntPtr device, ref Guid guid, uint index, ref DeviceInterface item);
    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", SetLastError = true)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr info, ref DeviceInterface item, IntPtr detail, uint size, out uint needed, IntPtr device);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr info);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
}
