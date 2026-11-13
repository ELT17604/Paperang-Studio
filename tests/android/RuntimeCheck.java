package com.elt.pprstudio.adc;

import android.app.Activity;
import android.app.Instrumentation;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.os.Bundle;
import android.os.SystemClock;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.util.Arrays;

public final class RuntimeCheck extends Instrumentation
{
	@Override public void onCreate(Bundle arguments)
	{
		super.onCreate(arguments);
		start();
	}

	@Override public void onStart()
	{
		Bundle result = new Bundle();
		try
		{
			checkRaster();
			checkHistory();
			checkWorkspace();
			checkDrawingAndLibrary();
			result.putString("stream", "\nPASS Android runtime: text, images, layers, crop, QR, RAW/PNG/raster1, " +
			                           "presets, drawings, materials and history\n");
			finish(Activity.RESULT_OK, result);
		}
		catch (Throwable failure)
		{
			result.putString("stream",
			                 "\nFAIL Android runtime: " + android.util.Log.getStackTraceString(failure) + "\n");
			finish(Activity.RESULT_CANCELED, result);
		}
	}

	private void checkRaster() throws Exception
	{
		var layout = PaperRenderer.LayoutOptions.DEFAULT;
		var tone = PaperRenderer.Tone.DEFAULT;
		byte[] text = PaperRenderer.text("纸条\nPaperang Studio", layout, tone);
		require(hasInk(text), "Chinese and Latin text");
		Bitmap photo = Bitmap.createBitmap(20, 30, Bitmap.Config.ARGB_8888);
		photo.setPixel(10, 15, Color.BLACK);
		byte[] pixels = PaperRenderer.image(photo, layout, tone);
		require(hasInk(pixels) && Raster.preview(pixels).getPixel(0, 0) == Color.WHITE, "photo and transparency");
		PaperComposition paper = new PaperComposition();
		paper.height = 720;
		paper.autoLength = false;
		PaperComposition.Layer label = new PaperComposition.Layer(PaperComposition.LABEL, 20, null);
		label.text = "标签";
		paper.layers.add(label);
		paper.layers.add(new PaperComposition.Layer(PaperComposition.PHOTO, 110, photo));
		paper.layers.add(new PaperComposition.Layer(PaperComposition.QR, 310, null));
		byte[] composition = paper.snapshot().raster(tone);
		require(composition.length == 720 * Raster.STRIDE && hasInk(composition), "layer composition");
		paper.autoLength = true;
		byte[] cropped = paper.snapshot().raster(tone);
		require(cropped.length < composition.length && cropped.length > 310 * Raster.STRIDE,
		        "trim trailing blank rows");
		require(Arrays.equals(cropped, Arrays.copyOf(composition, cropped.length)),
		        "preserve leading and intermediate rows");
		require(Arrays.equals(cropped, Raster.read(new ByteArrayInputStream(Raster.json(cropped)), false)),
		        "raster1 export round trip");
		QrMark.DEFAULT.validate("二维码", 220);
		try
		{
			QrMark.DEFAULT.validate("二维码", 12);
			throw new AssertionError("tiny QR accepted");
		}
		catch (IllegalArgumentException expected)
		{
			// Expected: insufficient room for the QR and its quiet zone.
		}
		require(Arrays.equals(composition, Raster.read(new ByteArrayInputStream(composition), true)), "RAW round trip");
		var digest = new StringBuilder();
		for (byte value : java.security.MessageDigest.getInstance("SHA-256").digest(composition))
		{
			digest.append(String.format(java.util.Locale.ROOT, "%02x", value & 255));
		}
		var envelope = new org.json.JSONObject()
		                   .put("type", "raster1")
		                   .put("schemaVersion", 1)
		                   .put("width", 576)
		                   .put("height", 720)
		                   .put("encoding", "base64")
		                   .put("bitOrder", "msb-first")
		                   .put("blackIsOne", true)
		                   .put("data", java.util.Base64.getEncoder().encodeToString(composition))
		                   .put("sha256", digest.toString());
		require(Arrays.equals(composition, readEnvelope(envelope)), "raster1 round trip");
		envelope.put("sha256", "bad");
		try
		{
			readEnvelope(envelope);
			throw new AssertionError("invalid raster1 checksum accepted");
		}
		catch (IllegalArgumentException expected)
		{
			require(expected.getMessage().contains("SHA-256"), "raster1 checksum error");
		}
		ByteArrayOutputStream png = new ByteArrayOutputStream();
		Bitmap preview = Raster.preview(composition);
		require(preview.compress(Bitmap.CompressFormat.PNG, 100, png), "PNG encoding");
		Bitmap decoded = android.graphics.BitmapFactory.decodeByteArray(png.toByteArray(), 0, png.size());
		require(preview.sameAs(decoded), "PNG pixel round trip");
		var preset = new WorkspacePreset(layout, tone, PrintOptions.DEFAULT);
		require(preset.equals(WorkspacePreset.read(preset.json())), "preset round trip");
	}

	private byte[] readEnvelope(org.json.JSONObject envelope) throws Exception
	{
		return Raster.read(
		    new ByteArrayInputStream(envelope.toString().getBytes(java.nio.charset.StandardCharsets.UTF_8)), false);
	}

	private void checkHistory() throws Exception
	{
		var prefs = getTargetContext().getSharedPreferences("runtime-check-history", 0);
		prefs.edit().clear().commit();
		try
		{
			var files = new LocalRasters(getTargetContext().getFilesDir(), "runtime-check-history");
			var drafts = new LocalRasters(getTargetContext().getFilesDir(), "runtime-check-drafts", ".json");
			var history = new PrintHistory(prefs, files, drafts);
			byte[] raster = new byte[Raster.STRIDE];
			raster[0] = (byte)0x80;
			var preset = new WorkspacePreset(PaperRenderer.LayoutOptions.DEFAULT, PaperRenderer.Tone.DEFAULT,
			                                 new PrintOptions(2, 70, 5, 60, true));
			var paper = new PaperComposition();
			var layer = new PaperComposition.Layer(PaperComposition.LABEL, 30, null);
			var text = new android.text.SpannableString("可编辑文字");
			text.setSpan(new android.text.style.StyleSpan(android.graphics.Typeface.BOLD), 0, 2, 33);
			layer.text = text;
			paper.layers.add(layer);
			byte[] draft = new PrintDraft(2, preset, "", null, paper).encode();
			String completed = history.start("打印", "1 份", raster, draft);
			history.finish(completed, "已发送");
			history.start("送纸", "20 行");
			var reopened = new PrintHistory(prefs, files, drafts);
			var restored = reopened.rows();
			require(restored.length() == 2, "history reload");
			require("已发送".equals(restored.optJSONObject(1).optString("status")), "record result");
			require(restored.optJSONObject(0).optString("status").contains("未知"), "interrupted history");
			require(Arrays.equals(raster, reopened.raster(completed)), "printed content survives reload");
			var restoredDraft = reopened.draft(completed);
			require(preset.equals(restoredDraft.preset()) && restoredDraft.paper().layers.size() == 1,
			        "restore editable job");
			var styled = (android.text.Spanned)restoredDraft.paper().layers.get(0).text;
			require(styled.getSpans(0, 2, android.text.style.StyleSpan.class).length == 1, "restore rich text");
			require(Arrays.equals(paper.raster(preset.tone()), restoredDraft.paper().raster(preset.tone())),
			        "restored layout pixels");
			require(reopened.delete(completed) && reopened.rows().length() == 1 && !files.contains(completed) &&
			            !drafts.contains(completed),
			        "delete history and local bytes");
		}
		finally
		{
			prefs.edit().clear().commit();
		}
	}

	private void checkWorkspace()
	{
		Intent intent = new Intent(getTargetContext(), MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
		Activity activity = startActivitySync(intent);
		try
		{
			View root = activity.getWindow().getDecorView();
			runOnMainSync(() -> {
				EditText editor = (EditText)find(root, EditText.class, null);
				require(editor != null, "text editor");
				editor.setText("Runtime preview");
			});
			boolean ready = false;
			for (int attempt = 0; attempt < 30 && !ready; attempt++)
			{
				SystemClock.sleep(100);
				boolean[] enabled = new boolean[1];
				runOnMainSync(() -> enabled[0] = find(root, Button.class, "原始点阵").isEnabled());
				ready = enabled[0];
			}
			require(ready, "live preview enables output");
			runOnMainSync(() -> {
				int rows = PaperRenderer
				               .text("Runtime preview", PaperRenderer.LayoutOptions.DEFAULT, PaperRenderer.Tone.DEFAULT)
				               .length /
				           Raster.STRIDE;
				String expectedLength = String.format(java.util.Locale.getDefault(), "纸长约 %.1f mm · %d 点 · 300 DPI",
				                                      rows / PaperRuler.DOTS_PER_MM, rows);
				require(find(root, TextView.class, expectedLength).isShown(), "ruler shows rendered paper length");
				require(!find(root, Button.class, "打印预览内容").isEnabled(), "print requires connection");
				find(root, Button.class, "连接打印机").performClick();
				require(find(root, Button.class, "扫描打印机").isShown(), "workspace connection shortcut");
				require(!find(root, Button.class, "自定义送纸").isEnabled(), "feed requires connection");
				find(root, Button.class, "工作台").performClick();
				require(find(root, Button.class, "原始点阵").isEnabled(), "navigation preserves preview");
				require(find(root, Button.class, "打印设置 · 1 份").isShown(), "workspace print settings");
				find(root, Button.class, "排版").performClick();
				require(find(root, Button.class, "添加对象") != null, "composition toolbar");
				find(root, Button.class, "素材").performClick();
				require(find(root, TextView.class, "已存素材").isShown(), "standalone material library");
				find(root, Button.class, "历史").performClick();
				require(find(root, TextView.class, "本地打印历史").isShown(), "history tab");
				find(root, Button.class, "打印机").performClick();
				require(find(root, Button.class, "关于与版本").isShown(), "build info entry");
				find(root, Button.class, "关于与版本").performClick();
			});
		}
		finally
		{
			runOnMainSync(activity::finish);
		}
	}

	private void checkDrawingAndLibrary() throws Exception
	{
		Activity activity = startActivitySync(
		    new Intent(getTargetContext(), MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
		byte[][] captured = new byte[1][];
		try
		{
			runOnMainSync(() -> {
				View root = activity.getWindow().getDecorView();
				find(root, Button.class, "手绘").performClick();
				require(find(root, Button.class, "全屏绘制").isShown(), "drawing entry");
				var pad = new DrawingEditor.Pad((MainActivity)activity);
				pad.layout(0, 0, 576, 1400);
				long now = SystemClock.uptimeMillis();
				sendStroke(pad, now, android.view.MotionEvent.ACTION_DOWN, 80, 320);
				sendStroke(pad, now, android.view.MotionEvent.ACTION_MOVE, 160, 400);
				sendStroke(pad, now, android.view.MotionEvent.ACTION_UP, 210, 450);
				captured[0] = pad.raster();
				sendStroke(pad, now, android.view.MotionEvent.ACTION_DOWN, 120, 300);
				sendPair(pad, now, android.view.MotionEvent.ACTION_POINTER_DOWN | 256, 120, 300, 260, 300);
				sendPair(pad, now, android.view.MotionEvent.ACTION_MOVE, 70, 280, 310, 280);
				sendPair(pad, now, android.view.MotionEvent.ACTION_MOVE, 70, 200, 310, 200);
				sendPair(pad, now, android.view.MotionEvent.ACTION_POINTER_UP | 256, 70, 200, 310, 200);
				sendStroke(pad, now, android.view.MotionEvent.ACTION_UP, 70, 200);
				require(Arrays.equals(captured[0], pad.raster()), "pinch and pan preserve printed pixels");
				pad.resetViewport();
				require(Arrays.equals(captured[0], pad.raster()), "fit paper preserves printed pixels");
				pad.undo();
				try
				{
					pad.raster();
					throw new AssertionError("undo left ink");
				}
				catch (IllegalArgumentException expected)
				{
					// Empty paper is not printable.
				}
			});
			require(hasInk(captured[0]) && captured[0].length < 600 * Raster.STRIDE, "touch drawing and trim");
			var index = getTargetContext().getSharedPreferences("runtime-materials", 0);
			var files = new LocalRasters(getTargetContext().getFilesDir(), "runtime-materials");
			var library = new MaterialLibrary(index, files);
			library.save("手绘测试", captured[0]);
			var reloaded = new MaterialLibrary(index, files);
			var entry = reloaded.entries().get(0);
			require(Arrays.equals(captured[0], reloaded.read(entry)), "drawing library reload");
			Bitmap transparent = Raster.bitmap(reloaded.read(entry), Color.TRANSPARENT);
			require(transparent.getPixel(0, 0) == Color.TRANSPARENT, "material insertion preserves transparency");
			require(reloaded.delete(entry) && reloaded.entries().isEmpty() && !files.contains(entry.id()),
			        "delete material");
		}
		finally
		{
			runOnMainSync(activity::finish);
		}
	}

	private static void sendPair(View pad, long now, int action, float x1, float y1, float x2, float y2)
	{
		var properties = new android.view.MotionEvent.PointerProperties[2];
		var coordinates = new android.view.MotionEvent.PointerCoords[2];
		for (int i = 0; i < 2; i++)
		{
			properties[i] = new android.view.MotionEvent.PointerProperties();
			properties[i].id = i;
			properties[i].toolType = android.view.MotionEvent.TOOL_TYPE_FINGER;
			coordinates[i] = new android.view.MotionEvent.PointerCoords();
			coordinates[i].x = i == 0 ? x1 : x2;
			coordinates[i].y = i == 0 ? y1 : y2;
		}
		var event = android.view.MotionEvent.obtain(now, now, action, 2, properties, coordinates, 0, 0, 1, 1, 0, 0,
		                                            android.view.InputDevice.SOURCE_TOUCHSCREEN, 0);
		pad.dispatchTouchEvent(event);
		event.recycle();
	}

	private static void sendStroke(View pad, long now, int action, float x, float y)
	{
		var event = android.view.MotionEvent.obtain(now, now, action, x, y, 0);
		pad.dispatchTouchEvent(event);
		event.recycle();
	}

	private static View find(View view, Class<?> type, String text)
	{
		if (type.isInstance(view) && (text == null || text.contentEquals(((TextView)view).getText())))
			return view;
		if (view instanceof ViewGroup group)
		{
			for (int i = 0; i < group.getChildCount(); i++)
			{
				View match = find(group.getChildAt(i), type, text);
				if (match != null)
					return match;
			}
		}
		return null;
	}

	private static boolean hasInk(byte[] bytes)
	{
		for (byte b : bytes)
			if (b != 0)
				return true;
		return false;
	}

	private static void require(boolean condition, String message)
	{
		if (!condition)
			throw new AssertionError(message);
	}
}
