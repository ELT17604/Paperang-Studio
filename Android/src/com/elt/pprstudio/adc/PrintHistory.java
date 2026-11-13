package com.elt.pprstudio.adc;

import android.content.SharedPreferences;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

final class PrintHistory
{
	private final SharedPreferences prefs;
	private final LocalRasters contents;
	private final LocalRasters drafts;

	PrintHistory(SharedPreferences prefs, LocalRasters contents, LocalRasters drafts)
	{
		this.prefs = prefs;
		this.contents = contents;
		this.drafts = drafts;
		JSONArray rows = rows();
		boolean changed = false;
		for (int i = 0; i < rows.length(); i++)
		{
			JSONObject row = rows.optJSONObject(i);
			if (row != null && "发送中".equals(row.optString("status")))
			{
				put(row, "status", "应用退出，发送结果未知");
				changed = true;
			}
		}
		if (changed)
			save(rows);
	}

	JSONArray rows()
	{
		try
		{
			return new JSONArray(prefs.getString("history", "[]"));
		}
		catch (JSONException error)
		{
			return new JSONArray();
		}
	}

	String start(String title, String detail)
	{
		try
		{
			return start(title, detail, null);
		}
		catch (java.io.IOException error)
		{
			throw new IllegalStateException(error);
		}
	}

	String start(String title, String detail, byte[] raster) throws java.io.IOException
	{
		return start(title, detail, raster, null);
	}

	String start(String title, String detail, byte[] raster, byte[] draft) throws java.io.IOException
	{
		String id = java.util.UUID.randomUUID().toString();
		if (raster != null)
			contents.save(id, raster);
		try
		{
			if (draft != null)
				drafts.save(id, draft);
		}
		catch (java.io.IOException error)
		{
			contents.delete(id);
			throw error;
		}
		JSONObject row = new JSONObject();
		put(row, "id", id);
		put(row, "title", title);
		put(row, "detail", detail);
		put(row, "time", System.currentTimeMillis());
		put(row, "status", "发送中");
		JSONArray previous = rows();
		JSONArray next = new JSONArray().put(row);
		for (int i = 0; i < previous.length(); i++)
			next.put(previous.opt(i));
		if (!prefs.edit().putString("history", next.toString()).commit())
		{
			contents.delete(id);
			drafts.delete(id);
			throw new java.io.IOException("无法保存打印记录");
		}
		return id;
	}

	void finish(String id, String status)
	{
		JSONArray rows = rows();
		for (int i = 0; i < rows.length(); i++)
		{
			JSONObject row = rows.optJSONObject(i);
			if (row != null && row.optString("id").equals(id))
			{
				put(row, "status", status);
				save(rows);
				return;
			}
		}
	}

	boolean hasRaster(String id)
	{
		return contents.contains(id);
	}

	byte[] raster(String id) throws Exception
	{
		return contents.read(id);
	}

	PrintDraft draft(String id) throws Exception
	{
		return drafts.contains(id) ? PrintDraft.decode(drafts.readDraft(id)) : null;
	}

	boolean delete(String id)
	{
		JSONArray rows = rows();
		JSONArray keep = new JSONArray();
		for (int i = 0; i < rows.length(); i++)
		{
			JSONObject row = rows.optJSONObject(i);
			if (row != null && !id.equals(row.optString("id")))
				keep.put(row);
		}
		if (!prefs.edit().putString("history", keep.toString()).commit())
			return false;
		contents.delete(id);
		drafts.delete(id);
		return true;
	}

	private void save(JSONArray rows)
	{
		prefs.edit().putString("history", rows.toString()).apply();
	}

	private static void put(JSONObject row, String key, Object value)
	{
		try
		{
			row.put(key, value);
		}
		catch (JSONException error)
		{
			throw new IllegalArgumentException(error);
		}
	}
}
