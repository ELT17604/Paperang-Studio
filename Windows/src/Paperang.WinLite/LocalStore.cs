using Microsoft.Win32;
using Paperang.Core;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Paperang.WinLite;

internal sealed class LocalStore : IJournal
{
    public static string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELT", "PaperangStudioWinLite");
    public AppSettings LoadSettings() => Read<AppSettings>("settings.dat") ?? new();
    public void SaveSettings(AppSettings value) => Write("settings.dat", value);
    public PendingResult? Load() => Read<PendingResult>("pending.dat");
    public void Save(PendingResult? value)
    {
        if (value == null) { File.Delete(Path.Combine(Root, "pending.dat")); return; }
        Write("pending.dat", value);
    }
    private static T? Read<T>(string name)
    {
        string path = Path.Combine(Root, name);
        if (!File.Exists(path)) return default;
        return JsonSerializer.Deserialize<T>(Protect(File.ReadAllBytes(path), false), RelayClient.Json)
            ?? throw new InvalidDataException("本地数据无效");
    }
    private static void Write<T>(string name, T value)
    {
        Directory.CreateDirectory(Root);
        byte[] bytes = Protect(JsonSerializer.SerializeToUtf8Bytes(value, RelayClient.Json), true);
        string destination = Path.Combine(Root, name), temp = destination + ".tmp";
        using (var f = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { f.Write(bytes); f.Flush(true); }
        File.Move(temp, destination, true);
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
    public static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(PathName); return key?.GetValue(Name) is string; }
    }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(PathName);
        if (enabled) key.SetValue(Name, "\"" + Environment.ProcessPath + "\" --background");
        else key.DeleteValue(Name, false);
    }
}
