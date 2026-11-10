using System.Buffers.Binary;

namespace Paperang.Core;

public sealed record Frame(byte Command, byte Index, byte[] Payload);

public static class Wire
{
    public static uint Crc(ReadOnlySpan<byte> data, uint seed = 0)
    {
        uint c = seed ^ 0xffffffff;
        foreach (byte b in data) { c ^= b; for (int i = 0; i < 8; i++) c = (c >> 1) ^ ((c & 1) != 0 ? 0xedb88320u : 0); }
        return c ^ 0xffffffff;
    }

    public static byte[] Encode(byte command, byte[] payload, uint seed = 0, byte index = 0)
    {
        if (payload.Length > 1023) throw new ArgumentException("载荷超过 1023 字节");
        var data = new byte[payload.Length + 10];
        data[0] = 2; data[1] = command; data[2] = index;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(3), (ushort)payload.Length);
        payload.CopyTo(data, 5);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(5 + payload.Length), Crc(payload, seed));
        data[^1] = 3;
        return data;
    }
}

public sealed class FrameDecoder(uint seed = 0, bool recoverPartial = false)
{
    private readonly List<byte> buffer = [];
    public IEnumerable<Frame> Push(byte[] data)
    {
        buffer.AddRange(data);
        var result = new List<Frame>();
        while (buffer.Count >= 10)
        {
            int n = buffer[3] | buffer[4] << 8;
            if (buffer[0] != 2 || n > 1023) { buffer.RemoveAt(0); continue; }
            if (buffer.Count < n + 10)
            {
                int next = -1;
                if (recoverPartial) for (int i = 1; i + 10 <= buffer.Count; i++)
                {
                    if (buffer[i] != 2) continue;
                    int size = buffer[i + 3] | buffer[i + 4] << 8;
                    if (size > 1023 || i + size + 10 > buffer.Count || buffer[i + size + 9] != 3) continue;
                    if (Wire.Crc(buffer.GetRange(i + 5, size).ToArray(), seed) == BinaryPrimitives.ReadUInt32LittleEndian(buffer.GetRange(i + 5 + size, 4).ToArray())) { next = i; break; }
                }
                if (next < 0) break;
                buffer.RemoveRange(0, next); continue;
            }
            if (buffer[n + 9] != 3) { buffer.RemoveAt(0); continue; }
            byte[] p = buffer.GetRange(5, n).ToArray();
            uint crc = BinaryPrimitives.ReadUInt32LittleEndian(buffer.GetRange(5 + n, 4).ToArray());
            if (Wire.Crc(p, seed) == crc) result.Add(new(buffer[1], buffer[2], p));
            else if (recoverPartial) { buffer.RemoveAt(0); continue; }
            buffer.RemoveRange(0, n + 10);
        }
        return result;
    }
}
