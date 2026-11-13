package com.elt.pprstudio.adc;

import android.content.SharedPreferences;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

final class MaterialLibrary
{
	record Entry(String id, String name)
	{
	}

	private final SharedPreferences index;
	private final LocalRasters contents;

	MaterialLibrary(SharedPreferences index, LocalRasters contents)
	{
		this.index = index;
		this.contents = contents;
	}

	List<Entry> entries()
	{
		var entries = new ArrayList<Entry>();
		for (var entry : index.getAll().entrySet())
		{
			if (entry.getValue() instanceof String name && contents.contains(entry.getKey()))
			{
				entries.add(new Entry(entry.getKey(), name));
			}
		}
		entries.sort(java.util.Comparator.comparing(Entry::name).thenComparing(Entry::id));
		return entries;
	}

	void save(String name, byte[] bytes) throws IOException
	{
		if (index.getAll().size() >= 100)
			throw new IllegalArgumentException("素材库已满，请先删除旧素材");
		String id = UUID.randomUUID().toString();
		contents.save(id, bytes);
		if (!index.edit().putString(id, name).commit())
		{
			contents.delete(id);
			throw new IOException("无法保存素材索引");
		}
	}

	byte[] read(Entry entry) throws Exception
	{
		return contents.read(entry.id());
	}

	boolean delete(Entry entry)
	{
		if (!index.edit().remove(entry.id()).commit())
			return false;
		contents.delete(entry.id());
		return true;
	}
}
