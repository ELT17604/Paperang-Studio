package com.elt.pprstudio.adc;

import android.util.AtomicFile;
import java.io.File;
import java.io.IOException;

final class LocalRasters
{
	private final File directory;
	private final String extension;

	LocalRasters(File files, String name)
	{
		this(files, name, ".raw");
	}

	LocalRasters(File files, String name, String extension)
	{
		directory = new File(files, name);
		this.extension = extension;
	}

	void save(String id, byte[] bytes) throws IOException
	{
		var file = file(id);
		var stream = file.startWrite();
		try
		{
			stream.write(bytes);
			file.finishWrite(stream);
		}
		catch (IOException error)
		{
			file.failWrite(stream);
			throw error;
		}
	}

	byte[] read(String id) throws Exception
	{
		try (var stream = file(id).openRead())
		{
			return Raster.read(stream, true);
		}
	}

	byte[] readDraft(String id) throws IOException
	{
		try (var stream = file(id).openRead())
		{
			return stream.readAllBytes();
		}
	}

	boolean contains(String id)
	{
		return file(id).getBaseFile().isFile();
	}

	void delete(String id)
	{
		file(id).delete();
	}

	private AtomicFile file(String id)
	{
		if (!id.matches("[0-9a-f-]{36}"))
			throw new IllegalArgumentException("无效的本地记录");
		return new AtomicFile(new File(directory, id + extension));
	}
}
