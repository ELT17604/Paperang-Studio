package com.elt.pprstudio.adc;

import java.util.Arrays;
import java.util.function.BiConsumer;

final class Wire
{
	static int crc(byte[] bytes, int seed)
	{
		int crc = seed ^ -1;
		for (byte value : bytes)
		{
			crc ^= value & 255;
			for (int bit = 0; bit < 8; bit++)
			{
				crc = (crc >>> 1) ^ ((crc & 1) == 1 ? 0xedb88320 : 0);
			}
		}
		return crc ^ -1;
	}

	static int u16(byte[] bytes, int offset)
	{
		return (bytes[offset] & 255) | ((bytes[offset + 1] & 255) << 8);
	}

	static byte[] frame(int command, int index, byte[] payload, int seed)
	{
		byte[] frame = new byte[payload.length + 10];
		frame[0] = 2;
		frame[1] = (byte)command;
		frame[2] = (byte)index;
		frame[3] = (byte)payload.length;
		frame[4] = (byte)(payload.length >>> 8);
		System.arraycopy(payload, 0, frame, 5, payload.length);
		int checksum = crc(payload, seed);
		for (int i = 0; i < 4; i++)
		{
			frame[5 + payload.length + i] = (byte)(checksum >>> (i * 8));
		}
		frame[frame.length - 1] = 3;
		return frame;
	}

	static final class Decoder
	{
		private byte[] buffer = new byte[0];

		void push(byte[] bytes, BiConsumer<Integer, byte[]> receive)
		{
			int start = buffer.length;
			buffer = Arrays.copyOf(buffer, start + bytes.length);
			System.arraycopy(bytes, 0, buffer, start, bytes.length);
			int offset = 0;
			while (buffer.length - offset >= 10)
			{
				int length = u16(buffer, offset + 3);
				if (buffer[offset] != 2 || length > 1023)
				{
					offset++;
					continue;
				}
				int end = offset + length + 10;
				if (end > buffer.length)
					break;
				if (buffer[end - 1] != 3)
				{
					offset++;
					continue;
				}
				byte[] payload = Arrays.copyOfRange(buffer, offset + 5, end - 5);
				int checksum = u16(buffer, end - 5) | (u16(buffer, end - 3) << 16);
				if (crc(payload, 0) == checksum)
					receive.accept(buffer[offset + 1] & 255, payload);
				offset = end;
			}
			buffer = Arrays.copyOfRange(buffer, offset, buffer.length);
		}
	}
}
