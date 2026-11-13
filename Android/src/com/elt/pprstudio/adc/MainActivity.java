package com.elt.pprstudio.adc;

import android.Manifest;
import android.annotation.SuppressLint;
import android.app.Activity;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothManager;
import android.bluetooth.le.BluetoothLeScanner;
import android.bluetooth.le.ScanCallback;
import android.bluetooth.le.ScanResult;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.ParcelUuid;
import android.provider.OpenableColumns;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import java.util.HashSet;
import java.util.Locale;
import java.util.Set;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

@SuppressLint("MissingPermission") public final class MainActivity extends Activity
{
	private final Handler main = new Handler(Looper.getMainLooper());
	private final ExecutorService imports = Executors.newSingleThreadExecutor();
	private final Set<String> found = new HashSet<>();
	private BluetoothLeScanner scanner;
	private ScanCallback scan;
	private P2Session session;
	private LinearLayout devices;
	private TextView status;
	private TextView workStatus;
	private TextView document;
	private ImageView preview;
	private Button print;
	private Button disconnect;
	private Button refreshPrinter;
	private Button scanPrinter;
	private Button connectFromWorkspace;
	private ViewGroup paperTray;
	private Button printSettingsButton;
	private byte[] raster;
	private byte[] exporting;
	private int importGeneration;
	private Workspace workspace;
	private PrintHistory history;
	private MaterialLibrary materials;
	private PrintOptions settings = PrintOptions.DEFAULT;
	private Button feed;
	private Button inspect;
	private Button export;
	private LinearLayout historyPanel;
	private LinearLayout materialPanel;
	private java.util.function.IntConsumer openPage;
	private boolean preparingPrint;
	private Runnable showPreview;
	private WorkspacePreset exportingPreset;

	@Override public void onCreate(Bundle state)
	{
		super.onCreate(state);
		history = new PrintHistory(getSharedPreferences("print-history", MODE_PRIVATE),
		                           new LocalRasters(getFilesDir(), "print-history"),
		                           new LocalRasters(getFilesDir(), "print-drafts", ".json"));
		materials = new MaterialLibrary(getSharedPreferences("drawing-library", MODE_PRIVATE),
		                                new LocalRasters(getFilesDir(), "drawing-library"));
		ScrollView scroll = new ScrollView(this);
		scroll.setBackgroundColor(getColor(android.R.color.system_neutral1_10));
		LinearLayout page = column();
		page.setPadding(dp(20), dp(16), dp(20), dp(24));
		LinearLayout screen = column();
		screen.setBackgroundColor(getColor(android.R.color.system_neutral1_10));
		screen.setOnApplyWindowInsetsListener((view, insets) -> {
			var bars = insets.getInsets(android.view.WindowInsets.Type.systemBars() |
			                            android.view.WindowInsets.Type.displayCutout());
			int keyboard = insets.getInsets(android.view.WindowInsets.Type.ime()).bottom;
			view.setPadding(bars.left, bars.top, bars.right, Math.max(bars.bottom, keyboard));
			return insets;
		});
		getWindow().setSoftInputMode(android.view.WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
		scroll.setFillViewport(true);
		scroll.setClipToPadding(false);
		screen.addView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
		var content = new android.widget.FrameLayout(this);
		content.addView(page, new android.widget.FrameLayout.LayoutParams(-1, -2, android.view.Gravity.TOP | android.view.Gravity.CENTER_HORIZONTAL));
		content.addOnLayoutChangeListener((view, left, top, right, bottom, oldLeft, oldTop, oldRight, oldBottom) ->
		{
			int width = Math.min(right - left, dp(720));
			var params = page.getLayoutParams();
			if (width > 0 && params.width != width)
			{
				params.width = width;
				page.setLayoutParams(params);
			}
		});
		scroll.addView(content, new ScrollView.LayoutParams(-1, -2));
		setContentView(screen);
		LinearLayout heading = new LinearLayout(this);
		heading.setGravity(android.view.Gravity.CENTER_VERTICAL);
		ImageView mark = new ImageView(this);
		int icon = getApplicationInfo().icon;
		if (icon != 0)
			mark.setImageResource(icon);
		mark.setImportantForAccessibility(android.view.View.IMPORTANT_FOR_ACCESSIBILITY_NO);
		heading.addView(mark, new LinearLayout.LayoutParams(dp(44), dp(44)));
		LinearLayout titleGroup = column();
		titleGroup.setPadding(dp(12), 0, 0, 0);
		TextView title = label("Paperang Studio", 26);
		title.setTypeface(null, android.graphics.Typeface.BOLD);
		titleGroup.addView(title);
		TextView subtitle = label("Paperang P2 · 蓝牙打印", 13);
		subtitle.setTextColor(getColor(android.R.color.system_neutral2_700));
		titleGroup.addView(subtitle);
		heading.addView(titleGroup, new LinearLayout.LayoutParams(0, -2, 1));
		page.addView(heading);

		LinearLayout printerPanel = card(page, "打印机");
		status = label("连接 P2 后即可打印。", 16);
		status.setBackground(shape(getColor(android.R.color.system_neutral1_100), 18));
		status.setPadding(dp(14), dp(12), dp(14), dp(12));
		status.setAccessibilityLiveRegion(TextView.ACCESSIBILITY_LIVE_REGION_POLITE);
		printerPanel.addView(status);
		scanPrinter = button("扫描打印机", this::startScan);
		printerPanel.addView(scanPrinter);
		devices = column();
		printerPanel.addView(devices);
		disconnect = button("断开连接", () -> disconnect("已断开。打印机已接收的数据可能继续打印。"));

		refreshPrinter = button("刷新状态", () -> {
			if (session != null)
				session.refreshDevice();
		});
		printerPanel.addView(actionRow(refreshPrinter, disconnect));
		feed = button("自定义送纸", this::feedPaper);
		printerPanel.addView(actionRow(button("打印设置", this::printSettings), feed));
		printerPanel.addView(button("关于与版本", this::showVersion));

		LinearLayout work = card(page, "打印工作台");
		workspace = new Workspace(this, work);
		LinearLayout previewPanel = card(page, "打印预览");
		document = label("输入文字或导入内容以生成预览。", 16);
		document.setAccessibilityLiveRegion(TextView.ACCESSIBILITY_LIVE_REGION_POLITE);
		previewPanel.addView(document);
		preview = new ImageView(this);
		preview.setAdjustViewBounds(true);
		preview.setContentDescription("打印预览");
		preview.setOnClickListener(view -> inspectPreview());
		preview.setMaxHeight(dp(400));
		LinearLayout tray = column();
		paperTray = tray;
		tray.setPadding(dp(14), dp(14), dp(14), dp(16));
		tray.setBackground(shape(getColor(android.R.color.system_neutral1_200), 20));
		LinearLayout paper = column();
		paper.setBackground(paperSurface());
		paper.setPadding(dp(8), dp(9), dp(8), dp(16));
		paper.addView(preview, new LinearLayout.LayoutParams(-1, ViewGroup.LayoutParams.WRAP_CONTENT));
		tray.addView(paper);
		previewPanel.addView(tray);
		print = button("打印预览内容", this::confirmPrint);
		print.setTextColor(getColor(android.R.color.system_accent1_10));
		print.setBackground(new android.graphics.drawable.RippleDrawable(
		    android.content.res.ColorStateList.valueOf(0x33ffffff),
		    shape(getColor(android.R.color.system_accent1_700), 24), null));
		previewPanel.addView(print);
		inspect = button("原始点阵", this::inspectPreview);
		workStatus = label("连接 P2 后即可打印。", 16);
		workStatus.setAccessibilityLiveRegion(TextView.ACCESSIBILITY_LIVE_REGION_POLITE);
		previewPanel.addView(workStatus);
		connectFromWorkspace = button("连接打印机", () -> {});
		printSettingsButton = button("打印设置", this::printSettings);
		previewPanel.addView(actionRow(connectFromWorkspace, printSettingsButton));
		export = button("导出文件", this::exportPreview);
		previewPanel.addView(actionRow(inspect, export));
		showPreview = () -> scroll.smoothScrollTo(0, previewPanel.getTop());
		historyPanel = column();
		page.addView(historyPanel);
		materialPanel = column();
		page.addView(materialPanel);

		LinearLayout navigation = new LinearLayout(this);
		navigation.setPadding(dp(12), dp(4), dp(12), dp(8));
		navigation.setBackgroundColor(getColor(android.R.color.system_neutral1_50));
		Button editorTab = button("工作台", () -> {});
		Button printerTab = button("打印机", () -> {});
		Button historyTab = button("历史", () -> {});
		Button[] tabs = {editorTab, printerTab, historyTab, button("素材", () -> {})};
		for (Button tab : tabs)
		{
			var cell = new LinearLayout.LayoutParams(0, -2, 1);
			cell.setMargins(dp(4), 0, dp(4), 0);
			navigation.addView(tab, cell);
		}
		int[] currentPage = {-1};
		openPage = index ->
		{
			if (index == currentPage[0])
				return;
			boolean animate = currentPage[0] >= 0 && android.animation.ValueAnimator.areAnimatorsEnabled();
			currentPage[0] = index;
			printerPanel.setVisibility(index == 1 ? android.view.View.VISIBLE : android.view.View.GONE);
			work.setVisibility(index == 0 ? android.view.View.VISIBLE : android.view.View.GONE);
			previewPanel.setVisibility(work.getVisibility());
			historyPanel.setVisibility(index == 2 ? android.view.View.VISIBLE : android.view.View.GONE);
			if (index == 2)
				showHistory();
			materialPanel.setVisibility(index == 3 ? android.view.View.VISIBLE : android.view.View.GONE);
			if (index == 3)
				showMaterials();
			for (int i = 0; i < tabs.length; i++)
				setSelectedStyle(tabs[i], i == index);
			scroll.scrollTo(0, 0);
			page.animate().cancel();
			page.setAlpha(animate ? .72f : 1f);
			if (animate)
				page.animate().alpha(1f).setDuration(150).start();
		};
		for (int i = 0; i < tabs.length; i++)
		{
			int index = i;
			tabs[i].setOnClickListener(view -> openPage.accept(index));
		}
		connectFromWorkspace.setOnClickListener(view -> openPage.accept(1));
		screen.addView(navigation);
		openPage.accept(0);
		refreshButtons();
	}

	LinearLayout column()
	{
		LinearLayout layout = new LinearLayout(this);
		layout.setOrientation(LinearLayout.VERTICAL);
		return layout;
	}

	int dp(int value)
	{
		return Math.round(value * getResources().getDisplayMetrics().density);
	}

	TextView label(String text, int size)
	{
		TextView view = new TextView(this);
		view.setText(text);
		view.setTextSize(size);
		view.setTextColor(getColor(android.R.color.system_neutral1_900));
		view.setLineSpacing(dp(2), 1f);
		view.setPadding(0, dp(6), 0, dp(6));
		return view;
	}

	Button button(String text, Runnable action)
	{
		Button button = new Button(this);
		button.setText(text);
		button.setAllCaps(false);
		button.setStateListAnimator(null);
		button.setMinHeight(dp(48));
		button.setPadding(dp(8), dp(8), dp(8), dp(8));
		button.setMinimumWidth(0);
		button.setMinWidth(0);
		button.setTextColor(new android.content.res.ColorStateList(
		    new int[][] {new int[] {-android.R.attr.state_enabled}, new int[] {}},
		    new int[] {getColor(android.R.color.system_neutral1_600), getColor(android.R.color.system_accent1_800)}));
		var background = shape(getColor(android.R.color.system_accent1_100), 24);
		var ripple = android.content.res.ColorStateList.valueOf(0x24000000);
		button.setBackground(new android.graphics.drawable.RippleDrawable(ripple, background, null));
		LinearLayout.LayoutParams spacing = new LinearLayout.LayoutParams(-1, -2);
		spacing.setMargins(0, dp(4), 0, dp(4));
		button.setLayoutParams(spacing);
		button.setOnClickListener(view -> action.run());
		return button;
	}

	LinearLayout actionRow(Button... buttons)
	{
		LinearLayout row = new LinearLayout(this);
		for (Button button : buttons)
		{
			var cell = new LinearLayout.LayoutParams(0, -2, 1);
			cell.setMargins(dp(3), dp(3), dp(3), dp(3));
			row.addView(button, cell);
		}
		return row;
	}

	void setSelectedStyle(Button button, boolean selected)
	{
		button.setSelected(selected);
		int surface = getColor(selected ? android.R.color.system_accent1_700 : android.R.color.system_neutral1_100);
		button.setTextColor(
		    getColor(selected ? android.R.color.system_accent1_10 : android.R.color.system_accent1_800));
		button.setBackground(new android.graphics.drawable.RippleDrawable(
		    android.content.res.ColorStateList.valueOf(selected ? 0x33ffffff : 0x24000000), shape(surface, 24), null));
	}

	void jumpToPreview()
	{
		if (showPreview != null)
			showPreview.run();
	}

	private void showVersion()
	{
		try (var source = getAssets().open("source-build.txt"))
		{
			var info = getPackageManager().getPackageInfo(getPackageName(), 0);
			String build = new String(source.readNBytes(32), java.nio.charset.StandardCharsets.US_ASCII).trim();
			var details = column();
			var mark = new ImageView(this);
			mark.setImageResource(getApplicationInfo().icon);
			var iconSize = new LinearLayout.LayoutParams(dp(60), dp(60));
			iconSize.bottomMargin = dp(10);
			details.addView(mark, iconSize);
			var metadata = column();
			metadata.setBackground(shape(getColor(android.R.color.system_neutral1_100), 18));
			metadata.setPadding(dp(14), dp(8), dp(14), dp(8));
			metadata.addView(label("版本 · " + info.versionName, 15));
			metadata.addView(label("内部版本 · " + info.getLongVersionCode(), 14));
			TextView fingerprint = label("源码构建 · " + build, 14);
			fingerprint.setTextIsSelectable(true);
			metadata.addView(fingerprint);
			details.addView(metadata);
			TextView copyright = label("© 2026 ELT Group", 13);
			copyright.setTextColor(getColor(android.R.color.system_neutral2_700));
			copyright.setPadding(0, dp(12), 0, 0);
			details.addView(copyright);
			new StudioDialog(this)
			    .setTitle("关于 Paperang Studio")
			    .setView(details)
			    .setPositiveButton("完成", null)
			    .show();
		}
		catch (java.io.IOException | PackageManager.NameNotFoundException error)
		{
			show("无法读取构建信息");
		}
	}

	void show(String message)
	{
		status.setText(message);
		workStatus.setText(message);
		refreshButtons();
	}

	private void refreshButtons()
	{
		boolean printerReady = session != null && session.canPrint();
		print.setEnabled(!preparingPrint && raster != null && printerReady);
		print.setAlpha(print.isEnabled() ? 1f : .5f);
		boolean connected = session != null && session.isOpen();
		scanPrinter.setEnabled(!connected && scan == null);
		scanPrinter.setText(scan == null ? "扫描打印机" : "正在扫描…");
		connectFromWorkspace.setVisibility(connected ? android.view.View.GONE : android.view.View.VISIBLE);
		disconnect.setEnabled(connected);
		printSettingsButton.setText("打印设置 · " + settings.copies() + " 份");
		feed.setEnabled(printerReady);
		refreshPrinter.setEnabled(printerReady);
		inspect.setEnabled(raster != null);
		export.setEnabled(raster != null);
		paperTray.setVisibility(raster != null ? android.view.View.VISIBLE : android.view.View.GONE);
	}

	private void startScan()
	{
		if (checkSelfPermission(Manifest.permission.BLUETOOTH_SCAN) != PackageManager.PERMISSION_GRANTED ||
		    checkSelfPermission(Manifest.permission.BLUETOOTH_CONNECT) != PackageManager.PERMISSION_GRANTED)
		{
			requestPermissions(new String[] {Manifest.permission.BLUETOOTH_SCAN, Manifest.permission.BLUETOOTH_CONNECT},
			                   1);
			return;
		}
		if (session != null && session.isOpen())
		{
			show("请先断开当前打印机。");
			return;
		}
		BluetoothAdapter adapter = getSystemService(BluetoothManager.class).getAdapter();
		if (adapter == null || !adapter.isEnabled())
		{
			show("请先在系统设置中打开蓝牙。");
			return;
		}
		stopScan();
		devices.removeAllViews();
		found.clear();
		scanner = adapter.getBluetoothLeScanner();
		if (scanner == null)
		{
			show("蓝牙扫描暂时不可用。");
			return;
		}
		scan = new ScanCallback() {
			@Override public void onScanResult(int callbackType, ScanResult result)
			{
				main.post(() -> {
					if (scan != this || result.getScanRecord() == null)
						return;
					var record = result.getScanRecord();
					String name = record.getDeviceName();
					var services = record.getServiceUuids();
					String normalized = name == null ? "" : name.toUpperCase(Locale.ROOT);
					if (!(normalized.contains("PAPERANG") || normalized.equals("P2") ||
					      (services != null && services.contains(new ParcelUuid(P2Session.SERVICE)))))
						return;
					var device = result.getDevice();
					String address = device.getAddress();
					if (found.add(address))
					{
						String label = (name == null ? "Paperang 候选设备" : name) + " · " + address;
						devices.addView(button(label, () -> connect(device)));
					}
				});
			}

			@Override public void onScanFailed(int code)
			{
				main.post(() -> {
					if (scan != this)
						return;
					stopScan();
					show("扫描失败（" + code + "）");
				});
			}
		};
		ScanCallback active = scan;
		try
		{
			scanner.startScan(active);
		}
		catch (IllegalStateException | SecurityException error)
		{
			stopScan();
			show("无法启动扫描，请检查蓝牙和附近设备权限。");
			return;
		}
		show("正在扫描附近打印机…");
		main.postDelayed(() -> {
			if (scan != active)
				return;
			stopScan();
			show(found.isEmpty() ? "未发现打印机。请确认 P2 已开机并断开其他客户端。" : "扫描结束，请选择打印机。");
		}, 12000);
	}

	private void stopScan()
	{
		if (scan == null)
			return;
		ScanCallback old = scan;
		scan = null;
		try
		{
			scanner.stopScan(old);
		}
		catch (IllegalStateException | SecurityException ignored)
		{
			// The adapter or permission can disappear while scanning.
		}
		scanner = null;
	}

	@Override public void onRequestPermissionsResult(int request, String[] permissions, int[] grants)
	{
		super.onRequestPermissionsResult(request, permissions, grants);
		if (request != 1)
			return;
		if (grants.length == 2 && grants[0] == PackageManager.PERMISSION_GRANTED &&
		    grants[1] == PackageManager.PERMISSION_GRANTED)
			startScan();
		else
			show("扫描和连接需要附近设备权限。");
	}

	private void connect(BluetoothDevice device)
	{
		if (session != null && session.isOpen())
			return;
		stopScan();
		devices.removeAllViews();
		try
		{
			session = new P2Session(this, device, this::show);
			show("正在连接并核对 P2 型号…");
		}
		catch (IllegalStateException | SecurityException error)
		{
			show("连接无法启动，请检查蓝牙和附近设备权限。");
		}
	}

	private void disconnect(String message)
	{
		if (session != null)
			session.close();
		session = null;
		show(message);
	}

	void openDocument()
	{
		Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
		intent.addCategory(Intent.CATEGORY_OPENABLE);
		intent.setType("*/*");
		startActivityForResult(intent, 2);
	}

	@Override protected void onActivityResult(int request, int result, Intent data)
	{
		super.onActivityResult(request, result, data);
		if (result != RESULT_OK || data == null || data.getData() == null)
		{
			if (request == 5 || request == 6 || request == 9)
				exporting = null;
			if (request == 8)
				exportingPreset = null;
			return;
		}
		if (request == 3 || request == 4)
		{
			readImage(data.getData(), request == 4);
			return;
		}
		if (request == 5 || request == 6 || request == 9)
		{
			savePreview(data.getData(), request);
			return;
		}
		if (request == 7 || request == 8)
		{
			transferPreset(data.getData(), request == 8);
			return;
		}
		if (request == 2)
			importRaster(data.getData());
	}

	private void importRaster(android.net.Uri uri)
	{
		workspace.cancelPendingPreview();
		int generation = ++importGeneration;
		clearPreview("正在读取位图…");
		imports.execute(() -> {
			try
			{
				String name;
				try (var cursor =
				         getContentResolver().query(uri, new String[] {OpenableColumns.DISPLAY_NAME}, null, null, null))
				{
					name = cursor != null && cursor.moveToFirst() ? cursor.getString(0) : null;
				}
				String filename = name == null ? "导入位图" : name;
				boolean raw = filename.toLowerCase(Locale.ROOT).endsWith(".raw");
				byte[] bytes;
				try (var input = getContentResolver().openInputStream(uri))
				{
					if (input == null)
						throw new IllegalArgumentException("无法打开文件");
					bytes = Raster.read(input, raw);
				}
				main.post(() -> {
					if (!isDestroyed() && generation == importGeneration)
						workspace.setRaster(filename, bytes);
				});
			}
			catch (Exception error)
			{
				main.post(() -> {
					if (!isDestroyed() && generation == importGeneration)
					{
						document.setText("导入失败。请选择有效的 raster1 JSON 或 .raw 文件。");
					}
				});
			}
		});
	}

	private void confirmPrint()
	{
		byte[] selected = raster;
		P2Session printer = session;
		if (selected == null || printer == null || !printer.canPrint())
			return;
		new StudioDialog(this)
		    .setTitle("打印当前预览？")
		    .setMessage("打印 " + settings.copies() + " 份 · 浓度 " + settings.density() + " · 前送纸 " +
			            settings.before() + " 行 · 后送纸 " + settings.after() + " 行")
		    .setNegativeButton("取消", null)
		    .setPositiveButton(
		        "打印",
		        (dialog, which) -> {
			        if (session != printer || raster != selected || !printer.canPrint())
				        return;
			        String detail = settings.copies() + " 份 · 浓度 " + settings.density() + " · 前送纸 " +
					                settings.before() + " 行 · 后送纸 " + settings.after() + " 行" +
					                (settings.between() ? "（每份后）" : "（整批后）");
			        String title = document.getText().toString();
			        PrintOptions options = settings;
			        PrintDraft draft = workspace.draft(options);
			        preparingPrint = true;
			        show("正在保存打印任务");
			        imports.execute(() -> {
				        try
				        {
					        byte[] source = draft.encode();
					        main.post(() -> sendSavedPrint(printer, selected, title, detail, options, source));
				        }
				        catch (Exception error)
				        {
					        main.post(() -> {
						        preparingPrint = false;
						        if (!isDestroyed())
							        show("无法保存打印任务，请检查设备存储。");
					        });
				        }
			        });
		        })
		    .show();
	}

	private void sendSavedPrint(P2Session printer, byte[] bytes, String title, String detail, PrintOptions options,
	                            byte[] source)
	{
		preparingPrint = false;
		if (isDestroyed())
			return;
		if (session != printer || raster != bytes || !printer.canPrint())
		{
			show("预览或连接已变更，请重新确认打印。");
			return;
		}
		try
		{
			String id = history.start(title, detail, bytes, source);
			printer.print(bytes, options, outcome -> finishHistory(id, outcome));
		}
		catch (java.io.IOException error)
		{
			show("无法保存打印内容，请检查设备存储。");
		}
	}

	PaperSurface paperSurface()
	{
		return new PaperSurface(Color.WHITE, getColor(android.R.color.system_neutral1_300),
		                        getResources().getDisplayMetrics().density);
	}

	android.graphics.drawable.GradientDrawable shape(int color, int radius)
	{
		var shape = new android.graphics.drawable.GradientDrawable();
		shape.setColor(color);
		shape.setCornerRadius(dp(radius));
		return shape;
	}

	private LinearLayout card(LinearLayout parent, String title)
	{
		LinearLayout card = column();
		card.setBackground(shape(getColor(android.R.color.system_neutral1_50), 28));
		card.setPadding(dp(16), dp(12), dp(16), dp(16));
		var params = new LinearLayout.LayoutParams(-1, -2);
		params.topMargin = dp(16);
		parent.addView(card, params);
		TextView heading = label(title, 21);
		heading.setTypeface(android.graphics.Typeface.create("sans-serif-medium", android.graphics.Typeface.NORMAL));
		heading.setAccessibilityHeading(true);
		card.addView(heading);
		return card;
	}

	void invalidatePreview()
	{
		if (preview == null)
			return;
		++importGeneration;
		clearPreview("输入或导入内容以生成预览。");
	}

	private void clearPreview(String message)
	{
		raster = null;
		workspace.updatePaperLength(0);
		preview.setImageDrawable(null);
		document.setText(message);
		refreshButtons();
	}

	void generate(String title, java.util.function.Supplier<byte[]> renderer)
	{
		int generation = ++importGeneration;
		clearPreview("正在生成预览…");
		imports.execute(() -> {
			try
			{
				byte[] bytes = renderer.get();
				var image = Raster.preview(bytes);
				main.post(() -> {
					if (isDestroyed() || generation != importGeneration)
						return;
					raster = bytes;
					workspace.updatePaperLength(bytes.length / Raster.STRIDE);
					document.setText(title + " · 576 × " + (bytes.length / Raster.STRIDE));
					preview.setImageBitmap(image);
					refreshButtons();
				});
			}
			catch (IllegalArgumentException error)
			{
				main.post(() -> {
					if (!isDestroyed() && generation == importGeneration)
						document.setText(error.getMessage());
				});
			}
		});
	}

	void pickImage(boolean layer)
	{
		Intent picker = new Intent(Intent.ACTION_OPEN_DOCUMENT).setType("image/*");
		picker.addCategory(Intent.CATEGORY_OPENABLE);
		startActivityForResult(picker, layer ? 4 : 3);
	}

	private void readImage(android.net.Uri uri, boolean layer)
	{
		imports.execute(() -> {
			try
			{
				var source = android.graphics.ImageDecoder.createSource(getContentResolver(), uri);
				var bitmap = android.graphics.ImageDecoder.decodeBitmap(source, (decoder, info, input) -> {
					var size = info.getSize();
					if ((long)size.getWidth() * size.getHeight() > 40_000_000)
					{
						throw new IllegalArgumentException("图片需小于 4000 万像素");
					}
					int longest = Math.max(size.getWidth(), size.getHeight());
					if (longest > 2048)
					{
						double scale = 2048d / longest;
						decoder.setTargetSize(Math.max(1, (int)(size.getWidth() * scale)),
						                      Math.max(1, (int)(size.getHeight() * scale)));
					}
					decoder.setAllocator(android.graphics.ImageDecoder.ALLOCATOR_SOFTWARE);
				});
				main.post(() -> {
					if (!isDestroyed())
						workspace.setImage(bitmap, layer);
				});
			}
			catch (Exception error)
			{
				main.post(() -> {
					if (!isDestroyed())
						show("图片无法读取或尺寸过大");
				});
			}
		});
	}

	private void inspectPreview()
	{
		byte[] bytes = raster;
		if (bytes == null)
		{
			show("请先生成或导入预览");
			return;
		}
		inspectRaster(document.getText(), bytes);
	}

	private void inspectRaster(CharSequence title, byte[] bytes)
	{
		new StudioDialog(this).setTitle(title).setView(rasterView(bytes)).setPositiveButton("关闭", null).show();
	}

	private ScrollView rasterView(byte[] bytes)
	{
		var sheet = new ImageView(this);
		sheet.setScaleType(ImageView.ScaleType.FIT_XY);
		sheet.setBackgroundColor(Color.WHITE);
		sheet.setContentDescription("原始点阵打印预览");
		var drawable = new android.graphics.drawable.BitmapDrawable(getResources(), Raster.preview(bytes));
		drawable.setFilterBitmap(false);
		sheet.setImageDrawable(drawable);
		var horizontal = new android.widget.HorizontalScrollView(this);
		horizontal.addView(sheet,
		                   new android.widget.FrameLayout.LayoutParams(Raster.WIDTH, bytes.length / Raster.STRIDE));
		var paperScroll = new ScrollView(this);
		paperScroll.addView(horizontal, new ScrollView.LayoutParams(-1, -2));
		return paperScroll;
	}

	private void exportPreview()
	{
		if (raster == null)
		{
			show("请先生成或导入预览");
			return;
		}
		byte[] selected = raster;
		new StudioDialog(this)
		    .setTitle("导出打印预览")
		    .setItems(new String[] {"PNG", "RAW", "raster1 JSON"},
			          (dialog, index) -> exportRaster(selected, new int[] {5, 6, 9}[index]))
		    .show();
	}

	void exportRaster(byte[] bytes, int format)
	{
		exporting = bytes;
		Intent output = new Intent(Intent.ACTION_CREATE_DOCUMENT);
		output.addCategory(Intent.CATEGORY_OPENABLE);
		output.setType(format == 5 ? "image/png" : format == 9 ? "application/json" : "application/octet-stream");
		output.putExtra(Intent.EXTRA_TITLE, format == 5   ? "Paperang.png"
		                                    : format == 9 ? "Paperang.raster1.json"
		                                                  : "Paperang.raw");
		startActivityForResult(output, format);
	}

	private void savePreview(android.net.Uri uri, int format)
	{
		byte[] bytes = exporting;
		exporting = null;
		if (bytes == null)
			return;
		imports.execute(() -> {
			String result = "已导出打印预览";
			try (var output = getContentResolver().openOutputStream(uri))
			{
				if (output == null)
					throw new java.io.IOException();
				if (format == 5)
				{
					if (!Raster.preview(bytes).compress(android.graphics.Bitmap.CompressFormat.PNG, 100, output))
					{
						throw new java.io.IOException();
					}
				}
				else
					output.write(format == 9 ? Raster.json(bytes) : bytes);
			}
			catch (Exception error)
			{
				result = "导出失败";
			}
			String message = result;
			main.post(() -> {
				if (!isDestroyed())
					show(message);
			});
		});
	}

	void saveMaterial(byte[] bytes)
	{
		LinearLayout fields = column();
		var name = new android.widget.EditText(this);
		name.setSingleLine(true);
		name.setHint("素材名称");
		name.setFilters(new android.text.InputFilter[] {new android.text.InputFilter.LengthFilter(60)});
		fields.addView(name);
		workspace.form("保存到本地素材库", fields, () -> {
			String value = name.getText().toString().strip();
			if (value.isEmpty())
			{
				name.setError("请输入素材名称");
				throw new IllegalArgumentException("请输入素材名称");
			}
			try
			{
				materials.save(value, bytes);
				show("已保存素材 · " + value);
			}
			catch (java.io.IOException error)
			{
				throw new IllegalArgumentException("无法保存素材，请检查设备存储。", error);
			}
		});
	}

	void chooseMaterial(java.util.function.Consumer<byte[]> use)
	{
		var entries = materials.entries();
		if (entries.isEmpty())
		{
			new StudioDialog(this)
			    .setTitle("本地素材库")
			    .setMessage("完成手绘后，点按「存为素材」。")
			    .setPositiveButton("关闭", null)
			    .show();
			return;
		}
		new StudioDialog(this)
		    .setTitle("本地素材库")
		    .setItems(entries.stream().map(MaterialLibrary.Entry::name).toArray(String[] ::new),
			          (dialog, index) -> {
			              var entry = entries.get(index);
			              new StudioDialog(this)
			                  .setTitle(entry.name())
			                  .setItems(new String[] {"使用素材", "查看原始点阵", "导出 raster1 JSON", "删除素材"},
							            (actions, choice) -> {
				                            if (choice == 3)
				                            {
					                            confirmDelete("删除素材？",
												              () -> materials.delete(entry), () -> chooseMaterial(use));
					                            return;
				                            }
				                            try
				                            {
					                            byte[] bytes = materials.read(entry);
					                            if (choice == 0)
						                            use.accept(bytes);
					                            if (choice == 1)
						                            inspectRaster(entry.name(), bytes);
					                            if (choice == 2)
						                            exportRaster(bytes, 9);
				                            }
				                            catch (Exception error)
				                            {
					                            show("无法读取本地素材");
				                            }
			                            })
			                  .show();
		              })
		    .setNegativeButton("关闭", null)
		    .show();
	}

	private void confirmDelete(String title, java.util.function.BooleanSupplier delete, Runnable done)
	{
		new StudioDialog(this)
		    .setTitle(title)
		    .setNegativeButton("取消", null)
		    .setPositiveButton(
		        "删除",
		        (dialog, which) -> {
			        if (delete.getAsBoolean())
				        done.run();
			        else
				        new StudioDialog(this).setMessage("删除失败，请重试。").setPositiveButton("关闭", null).show();
		        })
		    .show();
	}

	void presets()
	{
		new StudioDialog(this)
		    .setTitle("工作台预设")
		    .setItems(new String[] {"默认", "照片", "标签", "线稿", "导入预设", "导出预设"},
			          (dialog, item) -> {
			              if (item >= 4)
			              {
				              Intent picker =
				                  new Intent(item == 4 ? Intent.ACTION_OPEN_DOCUMENT : Intent.ACTION_CREATE_DOCUMENT);
				              picker.addCategory(Intent.CATEGORY_OPENABLE);
				              picker.setType(item == 4 ? "*/*" : "application/json");
				              if (item == 5)
				              {
					              exportingPreset = workspace.preset(settings);
					              picker.putExtra(Intent.EXTRA_TITLE, "Paperang-preset.json");
				              }
				              startActivityForResult(picker, item == 4 ? 7 : 8);
				              return;
			              }
			              var layout = switch (item)
			              {
				              case 2 ->
					              new PaperRenderer.LayoutOptions(12, 8, 552, 1, 42, 4, "sans-serif", true, 0, 0, 400);
				              case 3 ->
					              new PaperRenderer.LayoutOptions(0, 12, 576, 1, 30, 4, "sans-serif", false, 0, 0, 400);
				              default -> PaperRenderer.LayoutOptions.DEFAULT;
			              };
			              var tone = switch (item)
			              {
				              case 1 -> new PaperRenderer.Tone(1, 128, 0, 1.1, 1, false);
				              case 3 -> new PaperRenderer.Tone(0, 160, 0, 1, 1, false);
				              default -> PaperRenderer.Tone.DEFAULT;
			              };
			              applyPreset(new WorkspacePreset(layout, tone, PrintOptions.DEFAULT));
		              })
		    .show();
	}

	private void applyPreset(WorkspacePreset preset)
	{
		settings = preset.print();
		workspace.apply(preset);
		show("工作台预设已应用");
	}

	private void transferPreset(android.net.Uri uri, boolean save)
	{
		WorkspacePreset exported = exportingPreset;
		exportingPreset = null;
		imports.execute(() -> {
			try
			{
				if (save)
				{
					if (exported == null)
						return;
					try (var stream = getContentResolver().openOutputStream(uri))
					{
						if (stream == null)
							throw new java.io.IOException();
						stream.write(exported.json().toString(2).getBytes(java.nio.charset.StandardCharsets.UTF_8));
					}
					main.post(() -> {
						if (!isDestroyed())
							show("已导出工作台预设");
					});
				}
				else
				{
					WorkspacePreset preset;
					try (var stream = getContentResolver().openInputStream(uri))
					{
						if (stream == null)
							throw new java.io.IOException();
						byte[] bytes = stream.readNBytes(128000);
						if (bytes.length == 128000)
							throw new IllegalArgumentException("预设超过大小上限");
						preset = WorkspacePreset.read(
						    new org.json.JSONObject(new String(bytes, java.nio.charset.StandardCharsets.UTF_8)));
					}
					main.post(() -> {
						if (!isDestroyed())
							applyPreset(preset);
					});
				}
			}
			catch (Exception error)
			{
				main.post(() -> {
					if (!isDestroyed())
						show(save ? "预设导出失败" : "无法读取工作台预设");
				});
			}
		});
	}

	private void printSettings()
	{
		LinearLayout form = column();
		var copies = workspace.field(form, "份数", settings.copies());
		var density = workspace.field(form, "打印浓度", settings.density());
		var before = workspace.field(form, "打印前送纸（行）", settings.before());
		var after = workspace.field(form, "打印后送纸（行）", settings.after());
		var between = workspace.check("每份之间也送纸", settings.between());
		form.addView(between);
		Button applyDensity = button("仅应用浓度并回读", () -> {
			if (session == null || !session.canPrint())
				return;
			int value;
			try
			{
				value = workspace.number(density, 0, 100);
			}
			catch (IllegalArgumentException error)
			{
				return;
			}
			P2Session printer = session;
			new StudioDialog(this)
			    .setTitle("将打印机浓度设为 " + value + "？")
			    .setNegativeButton("取消", null)
			    .setPositiveButton("应用",
				                   (dialog, which) -> {
				                       if (session == printer)
					                       printer.applyDensity(value);
			                       })
			    .show();
		});
		applyDensity.setEnabled(session != null && session.canPrint());
		form.addView(applyDensity);
		workspace.form("打印设置", form, () -> {
			settings = new PrintOptions(workspace.number(copies, 1, 20), workspace.number(density, 0, 100),
			                            workspace.number(before, 0, 2000), workspace.number(after, 0, 2000),
			                            between.isChecked());
			refreshButtons();
		});
	}

	private void feedPaper()
	{
		P2Session printer = session;
		if (printer == null || !printer.canPrint())
			return;
		LinearLayout form = column();
		var lines = workspace.field(form, "送纸行数", Math.max(1, settings.after()));
		workspace.form("确认送纸", form, () -> {
			int count = workspace.number(lines, 1, 2000);
			if (session != printer || !printer.canPrint())
				return;
			String id = history.start("手动送纸", count + " 行");
			printer.feedPaper(count, outcome -> finishHistory(id, outcome));
		});
	}

	private void finishHistory(String id, String outcome)
	{
		history.finish(id, outcome);
		if (historyPanel.getVisibility() == android.view.View.VISIBLE)
			showHistory();
	}

	private void showMaterials()
	{
		materialPanel.removeAllViews();
		var header = card(materialPanel, "已存素材");
		var entries = materials.entries();
		header.addView(
		    label(entries.isEmpty() ? "在手绘工作台完成绘制后，点按「存为素材」。" : entries.size() + " 项素材", 14));
		for (var entry : entries)
		{
			var row = card(materialPanel, entry.name());
			row.addView(actionRow(button("查看素材", () -> openMaterial(entry)),
			                      button("插入排版", () -> useMaterial(entry, bytes -> {
				                             openPage.accept(0);
				                             workspace.insertIllustration(bytes);
			                             }))));
		}
	}

	private void openMaterial(MaterialLibrary.Entry entry)
	{
		useMaterial(
		    entry,
		    bytes
		    -> new StudioDialog(this)
		           .setTitle(entry.name())
		           .setView(rasterView(bytes))
		           .setNegativeButton(
		               "删除",
		               (dialog,
					    which) -> confirmDelete("删除这项素材？", () -> materials.delete(entry), this::showMaterials))
		           .setPositiveButton("导出 JSON", (dialog, which) -> exportRaster(bytes, 9))
		           .show());
	}

	private void useMaterial(MaterialLibrary.Entry entry, java.util.function.Consumer<byte[]> use)
	{
		try
		{
			use.accept(materials.read(entry));
		}
		catch (Exception error)
		{
			new StudioDialog(this).setMessage("无法读取本地素材").setPositiveButton("关闭", null).show();
		}
	}

	private void showPrintedContent(String title, String id) throws Exception
	{
		byte[] bytes = history.raster(id);
		var body = column();
		body.addView(rasterView(bytes), new LinearLayout.LayoutParams(-1, 0, 1));
		var popup = new android.app.Dialog[1];
		body.addView(button("恢复任务并编辑", () -> {
			popup[0].dismiss();
			new StudioDialog(this)
			    .setTitle("恢复打印任务？")
			    .setMessage("将把历史任务载入工作台，替换该模式中当前的内容。")
			    .setNegativeButton("取消", null)
			    .setPositiveButton("恢复", (dialog, which) -> restorePrint(id, bytes))
			    .show();
		}));
		body.addView(button("作为插画插入排版", () -> {
			popup[0].dismiss();
			openPage.accept(0);
			workspace.insertIllustration(bytes);
		}));
		popup[0] = new StudioDialog(this).setTitle(title).setView(body).setPositiveButton("关闭", null).show();
	}

	private void restorePrint(String id, byte[] bytes)
	{
		imports.execute(() -> {
			try
			{
				PrintDraft draft = history.draft(id);
				main.post(() -> {
					if (isDestroyed())
						return;
					if (draft != null)
						settings = draft.preset().print();
					openPage.accept(0);
					workspace.restore(draft, bytes);
				});
			}
			catch (Exception error)
			{
				main.post(() -> {
					if (!isDestroyed())
						new StudioDialog(this).setMessage("无法读取可编辑任务").setPositiveButton("关闭", null).show();
				});
			}
		});
	}

	private void showHistory()
	{
		historyPanel.removeAllViews();
		var rows = history.rows();
		var heading = card(historyPanel, "本地打印历史");
		heading.addView(label(rows.length() == 0 ? "打印或送纸后，记录会出现在这里。" : rows.length() + " 条记录", 14));
		var date = java.text.DateFormat.getDateTimeInstance();
		for (int i = 0; i < rows.length(); i++)
		{
			var row = rows.optJSONObject(i);
			if (row == null)
				continue;
			var entry = card(historyPanel, row.optString("title"));
			entry.setBackground(paperSurface());
			entry.setPadding(dp(16), dp(12), dp(16), dp(21));
			entry.addView(label(row.optString("detail"), 14));
			entry.addView(label(row.optString("status"), 15));
			TextView time = label(date.format(new java.util.Date(row.optLong("time"))), 12);
			time.setTextColor(getColor(android.R.color.system_neutral2_700));
			entry.addView(time);
			String id = row.optString("id");
			Button delete =
			    button("删除记录",
				       () -> confirmDelete("删除这条记录及其打印内容？", () -> history.delete(id), this::showHistory));
			delete.setEnabled(!"发送中".equals(row.optString("status")));
			if (history.hasRaster(id))
			{
				Button content = button("查看打印内容", () -> {
					try
					{
						showPrintedContent(row.optString("title"), id);
					}
					catch (Exception error)
					{
						new StudioDialog(this).setMessage("无法读取打印内容").setPositiveButton("关闭", null).show();
					}
				});
				entry.addView(actionRow(content, delete));
			}
			else
				entry.addView(delete);
		}
	}

	@Override protected void onDestroy()
	{
		stopScan();
		if (session != null)
			session.close();
		workspace.cancelPendingPreview();
		imports.shutdownNow();
		main.removeCallbacksAndMessages(null);
		super.onDestroy();
	}
}
