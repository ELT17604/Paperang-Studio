using Microsoft.Win32;
using Paperang.Core;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Paperang.WinLite;

internal sealed class LocalStore : IJournal
{
    public static string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELT", "PaperangStudioWinLite");
    private readonly string directory;
    private readonly object ioLock = new();
    public LocalStore(string? directory = null) => this.directory = directory ?? Root;
    public AppSettings LoadSettings() => Read<AppSettings>("settings.dat") ?? new();
    public void SaveSettings(AppSettings value) => Write("settings.dat", value);
    public PendingResult? Load() => Read<PendingResult>("pending.dat");
    public void Save(PendingResult? value)
    {
        if (value == null) { lock (ioLock) File.Delete(Path.Combine(directory, "pending.dat")); return; }
        Write("pending.dat", value);
    }
    private T? Read<T>(string name)
    {
        lock (ioLock)
        {
        string path = Path.Combine(directory, name);
        if (!File.Exists(path)) return default;
        if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("本地配置文件过大");
        return JsonSerializer.Deserialize<T>(Protect(File.ReadAllBytes(path), false), RelayClient.Json)
            ?? throw new InvalidDataException("本地数据无效");
        }
    }
    private void Write<T>(string name, T value)
    {
        lock (ioLock)
        {
        Directory.CreateDirectory(directory);
        byte[] bytes = Protect(JsonSerializer.SerializeToUtf8Bytes(value, RelayClient.Json), true);
        string destination = Path.Combine(directory, name), temp = destination + ".tmp";
        using (var f = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { f.Write(bytes); f.Flush(true); }
        File.Move(temp, destination, true);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] Protect(byte[] bytes, bool encrypt)
    {
        var input = new Blob { Size = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        Blob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            bool ok = encrypt ? CryptProtectData(ref input, "Paperang Studio", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取或加密本地凭证；请使用原 Windows 用户");
            var result = new byte[output.Size]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally { Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) LocalFree(output.Data); }
    }
}

internal static class StartupRegistration
{
    private const string PathName = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "PaperangStudioWinLite";
    public static string? Command { get { using var key = Registry.CurrentUser.OpenSubKey(PathName); return key?.GetValue(Name) as string; } }
    public static bool Enabled
    {
        get => !string.IsNullOrWhiteSpace(Command);
    }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(PathName);
        if (enabled) key.SetValue(Name, BuildCommand(Environment.ProcessPath!));
        else key.DeleteValue(Name, false);
    }
    internal static string BuildCommand(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"') || !File.Exists(executable))
            throw new InvalidOperationException("开机启动程序路径无效");
        return "\"" + executable + "\" --background";
    }
    public static void RepairIfEnabled()
    {
        if (Enabled && Command != BuildCommand(Environment.ProcessPath!)) Set(true);
    }
}
