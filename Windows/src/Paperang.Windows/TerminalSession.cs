using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Paperang.Windows;

public sealed class TerminalSession : IAsyncDisposable
{
	private IntPtr console, process;
	private FileStream? input, output;
	private Task? reading, writing;
	private readonly BlockingCollection<byte[]> pending = new(32);
	private int stopped;
	private readonly object stopLock = new();
	private Task? stopping;
	public event Action<string>? Output;
	public event Action? Exited;
	public bool Running => process != IntPtr.Zero && Volatile.Read(ref stopped) == 0 &&
		GetExitCodeProcess(process, out uint code) && code == 259;

	public void Start(string directory, int columns, int rows, bool powershell = false, bool loadProfile = false)
	{
		if (console != IntPtr.Zero || stopped != 0) throw new InvalidOperationException("此终端会话不能重复启动。");
		if (!Directory.Exists(directory) || columns is < 20 or > 200 || rows is < 5 or > 100)
			throw new ArgumentException("终端工作目录或行列设置无效。");
		IntPtr inputRead = IntPtr.Zero, inputWrite = IntPtr.Zero, outputRead = IntPtr.Zero, outputWrite = IntPtr.Zero;
		IntPtr attributes = IntPtr.Zero;
		bool initialized = false;
		try
		{
			Check(CreatePipe(out inputRead, out inputWrite, IntPtr.Zero, 0));
			Check(CreatePipe(out outputRead, out outputWrite, IntPtr.Zero, 0));
			Marshal.ThrowExceptionForHR(CreatePseudoConsole(new((short)columns, (short)rows), inputRead, outputWrite, 0, out console));
			input = new FileStream(new SafeFileHandle(inputWrite, true), FileAccess.Write, 4096, false);
			inputWrite = IntPtr.Zero;
			output = new FileStream(new SafeFileHandle(outputRead, true), FileAccess.Read, 4096, false);
			outputRead = IntPtr.Zero;
			writing = Task.Factory.StartNew(WriteLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
			reading = Task.Factory.StartNew(ReadLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
			nuint length = 0;
			InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref length);
			attributes = Marshal.AllocHGlobal(checked((int)length));
			Check(InitializeProcThreadAttributeList(attributes, 1, 0, ref length));
			initialized = true;
			Check(UpdateProcThreadAttribute(attributes, 0, (IntPtr)0x20016, console, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero));
			var startup = new StartupInfoEx { Startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100 }, Attributes = attributes };
			string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
				powershell ? "WindowsPowerShell/v1.0/powershell.exe" : "cmd.exe");
			string arguments = powershell ? "-NoLogo " + (loadProfile ? "" : "-NoProfile ") : "/D /Q";
			Check(CreateProcess(executable, new StringBuilder("\"" + executable + "\" " + arguments), IntPtr.Zero, IntPtr.Zero,
				false, 0x00080000, IntPtr.Zero, directory, ref startup, out var created));
			process = created.Process;
			CloseHandle(created.Thread);
		}
		catch
		{
			_ = StopAsync();
			throw;
		}
		finally
		{
			if (initialized) DeleteProcThreadAttributeList(attributes);
			if (attributes != IntPtr.Zero) Marshal.FreeHGlobal(attributes);
			foreach (var handle in new[] { inputRead, inputWrite, outputRead, outputWrite })
				if (handle != IntPtr.Zero) CloseHandle(handle);
		}
	}
	public void Send(string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value);
		if (!Running || bytes.Length > 8192 || !pending.TryAdd(bytes))
			throw new InvalidOperationException("终端未运行、输入过长或输入队列已满。");
	}
	private void WriteLoop()
	{
		try { foreach (byte[] bytes in pending.GetConsumingEnumerable()) { input!.Write(bytes); input.Flush(); } }
		catch (IOException) { }
		catch (ObjectDisposedException) { }
	}
	private void ReadLoop()
	{
		try
		{
			using var reader = new StreamReader(output!, new UTF8Encoding(false, false), false, 4096, true);
			var buffer = new char[2048];
			int count;
			while ((count = reader.Read(buffer, 0, buffer.Length)) > 0) Output?.Invoke(new(buffer, 0, count));
		}
		catch (IOException) { }
		catch (ObjectDisposedException) { }
		finally { Exited?.Invoke(); }
	}
	public Task StopAsync()
	{
		lock (stopLock) return stopping ??= StopCoreAsync();
	}
	private async Task StopCoreAsync()
	{
		if (Interlocked.Exchange(ref stopped, 1) != 0) return;
		pending.CompleteAdding();
		var handle = Interlocked.Exchange(ref console, IntPtr.Zero);
		if (handle != IntPtr.Zero) await Task.Run(() => ClosePseudoConsole(handle)).ConfigureAwait(false);
		if (reading != null) await reading.ConfigureAwait(false);
		input?.Dispose();
		output?.Dispose();
		if (writing != null) await writing.ConfigureAwait(false);
		var child = Interlocked.Exchange(ref process, IntPtr.Zero);
		if (child != IntPtr.Zero) CloseHandle(child);
	}
	public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
	private static void Check(bool success) { if (!success) throw new Win32Exception(Marshal.GetLastWin32Error()); }

	[StructLayout(LayoutKind.Sequential)] private readonly record struct Coord(short X, short Y);
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct StartupInfo
	{
		public int Size;
		public IntPtr Reserved, Desktop, Title;
		public uint X, Y, Width, Height, WidthChars, HeightChars, Fill, Flags;
		public ushort Show, ReservedSize;
		public IntPtr ReservedBytes, Input, Output, Error;
	}
	[StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo Startup; public IntPtr Attributes; }
	[StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
	[DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CreatePipe(out IntPtr read, out IntPtr write, IntPtr attributes, uint size);
	[DllImport("kernel32.dll")] private static extern int CreatePseudoConsole(Coord size, IntPtr input, IntPtr output, uint flags, out IntPtr console);
	[DllImport("kernel32.dll")] private static extern void ClosePseudoConsole(IntPtr console);
	[DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint length);
	[DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, nuint length, IntPtr previous, IntPtr returned);
	[DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
	[DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes,
		IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment,
		string directory, ref StartupInfoEx startup, out ProcessInfo process);
	[DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
	[DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
}
