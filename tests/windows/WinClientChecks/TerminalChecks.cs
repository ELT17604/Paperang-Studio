using System.Text;
using Paperang.Core;
using Paperang.Windows;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private static async Task CheckTerminal()
	{
		var buffer = new TerminalBuffer();
		buffer.Append("abc\rX\n\x1b[31");
		buffer.Append("mred\x1b[0m\n");
		buffer.Append("SECRET\n", false);
		if (buffer.Transcript != "Xbc\nred\n" || buffer.Peek() != "Xbc\nred\n")
			throw new InvalidOperationException("Terminal carriage return, split escape or privacy parsing failed");
		string first = buffer.Peek(4);
		buffer.Commit(first);
		if (buffer.Peek() != "red\n") throw new InvalidOperationException("Terminal commit removed the wrong text");
		buffer.Discard();
		if (buffer.PendingCount != 0) throw new InvalidOperationException("Terminal discard did not clear pending text");
		var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var transcript = new StringBuilder();
		string expected = "PAPERANG_" + Environment.GetEnvironmentVariable("OS") + "_PTY";
		await using var session = new TerminalSession();
		session.Output += value =>
		{
			lock (transcript)
			{
				transcript.Append(value);
				if (transcript.ToString().Contains(expected, StringComparison.Ordinal)) complete.TrySetResult();
			}
		};
		await Task.Run(() => session.Start(Path.GetTempPath(), 80, 24));
		session.Send("echo PAPERANG_%OS%_PTY\r");
		await complete.Task.WaitAsync(TimeSpan.FromSeconds(8));
		session.Send("exit\r");
		var stopping = session.StopAsync();
		if (!ReferenceEquals(stopping, session.StopAsync()))
			throw new InvalidOperationException("Concurrent terminal stops did not share completion");
		await stopping.WaitAsync(TimeSpan.FromSeconds(8));
		if (session.Running) throw new InvalidOperationException("Terminal remained running after stop");
		Console.WriteLine("PASS real ConPTY shell input/output, shutdown, terminal escapes, privacy and buffered commit");
	}
}
