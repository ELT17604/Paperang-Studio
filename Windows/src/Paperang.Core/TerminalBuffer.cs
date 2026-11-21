using System.Text;

namespace Paperang.Core;

public sealed class TerminalBuffer
{
	private readonly StringBuilder transcript = new(), line = new(), pending = new(), sequence = new();
	private int cursor, escapeState;
	public string Transcript => transcript.ToString() + line;
	public int PendingCount => pending.Length;
	public bool Overflowed { get; private set; }
	public void Append(string value, bool record = true)
	{
		foreach (char symbol in value)
		{
			if (escapeState == 1)
			{
				escapeState = symbol == '[' ? 2 : symbol == ']' ? 3 : 0;
				sequence.Clear();
				continue;
			}
			if (escapeState == 2)
			{
				if (symbol is >= '@' and <= '~')
				{
					if (symbol == 'K' && cursor <= line.Length) line.Length = cursor;
					escapeState = 0;
				}
				else if (sequence.Length < 64) sequence.Append(symbol);
				else escapeState = 0;
				continue;
			}
			if (escapeState == 3)
			{
				if (symbol == '\a') escapeState = 0;
				else if (symbol == '\x1b') escapeState = 4;
				continue;
			}
			if (escapeState == 4) { escapeState = symbol == '\\' ? 0 : 3; continue; }
			if (symbol == '\x1b') { escapeState = 1; continue; }
			if (!record) continue;
			if (symbol == '\r') { cursor = 0; continue; }
			if (symbol == '\b') { cursor = Math.Max(0, cursor - 1); continue; }
			if (symbol == '\n')
			{
				string completed = line.ToString() + "\n";
				transcript.Append(completed);
				if (pending.Length + completed.Length <= 12000) pending.Append(completed);
				else Overflowed = true;
				line.Clear();
				cursor = 0;
				if (transcript.Length > 64000) transcript.Remove(0, transcript.Length - 64000);
				continue;
			}
			if (char.IsControl(symbol) && symbol != '\t') continue;
			if (line.Length >= 12000) { Overflowed = true; continue; }
			if (cursor < line.Length) line[cursor] = symbol;
			else line.Append(symbol);
			cursor++;
		}
	}
	public string Peek(int maximum = 1000)
	{
		int length = Math.Min(maximum, pending.Length);
		if (length > 0 && length < pending.Length && char.IsHighSurrogate(pending[length - 1])) length--;
		return pending.ToString(0, length);
	}
	public void Commit(string sent)
	{
		if (!pending.ToString().StartsWith(sent, StringComparison.Ordinal)) throw new InvalidOperationException("终端缓冲已改变。");
		pending.Remove(0, sent.Length);
	}
	public void Discard() { pending.Clear(); Overflowed = false; }
	public void ClearPartialLine() { line.Clear(); cursor = 0; }
}
