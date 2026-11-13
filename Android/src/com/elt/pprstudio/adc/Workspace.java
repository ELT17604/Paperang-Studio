package com.elt.pprstudio.adc;

import android.app.Dialog;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.text.InputType;
import android.text.SpannableString;
import android.text.Spanned;
import android.text.style.AbsoluteSizeSpan;
import android.text.style.StyleSpan;
import android.text.style.TypefaceSpan;
import android.view.MotionEvent;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.CheckBox;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.Spinner;
import com.google.zxing.qrcode.decoder.ErrorCorrectionLevel;

final class Workspace {
    private final MainActivity app;
    private final PaperComposition composition = new PaperComposition();
    private PaperRenderer.LayoutOptions layout = PaperRenderer.LayoutOptions.DEFAULT;
    private PaperRenderer.Tone tone = PaperRenderer.Tone.DEFAULT;
    private final EditText text;
    private final LinearLayout tools;
    private final PaperView paper;
    private int mode;
    private final android.widget.Button[] modes = new android.widget.Button[5];
    private final android.os.Handler main = new android.os.Handler(android.os.Looper.getMainLooper());
    private final Runnable renderLater = () -> render(false);
    private Bitmap image;
    private byte[] imported;
    private String importedName;
    private PaperComposition.Layer selected;
    private android.widget.Button editButton;
    private android.widget.Button duplicateButton;
    private boolean snap = true;
    private int printedRows;
    private android.widget.TextView lengthLabel;
    private View lengthGauge;
    private byte[] drawing;

    Workspace(MainActivity app, LinearLayout page) {
        this.app = app;
        String[] names = {"文字", "图片", "排版", "位图", "手绘"};
        for (int i = 0; i < modes.length; i++) {
            int index = i;
            modes[i] = app.button(names[i], () -> selectMode(index));
            modes[i].setPadding(app.dp(4), 0, app.dp(4), 0);
            app.setSelectedStyle(modes[i], i == mode);
        }
        page.addView(app.actionRow(modes));
        tools = app.column();
        page.addView(tools);
        text = new EditText(app);
        text.setHint("输入打印内容");
        text.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_MULTI_LINE);
        text.setMinLines(4);
        text.setGravity(android.view.Gravity.TOP | android.view.Gravity.START);
        text.setPadding(app.dp(16), app.dp(14), app.dp(16), app.dp(14));
        text.setBackground(app.paperSurface());
        text.setTextColor(app.getColor(android.R.color.system_neutral1_900));
        text.setHintTextColor(app.getColor(android.R.color.system_neutral2_700));
        text.addTextChangedListener(new android.text.TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) { changed(); }
            @Override public void afterTextChanged(android.text.Editable editable) {}
        });
        paper = new PaperView();
        showMode();

    }

    private void selectMode(int index) {
        if (index == mode) return;
        mode = index;
        for (int i = 0; i < modes.length; i++) app.setSelectedStyle(modes[i], i == mode);
        showMode();
        changed();
    }

    WorkspacePreset preset(PrintOptions print) {
        return new WorkspacePreset(layout, tone, print);
    }

    void apply(WorkspacePreset preset) {
        layout = preset.layout();
        tone = preset.tone();
        if (mode != 3) changed();
    }

    private void showMode() {
        tools.removeAllViews();
        lengthLabel = null;
        lengthGauge = null;
        switch (mode) {
            case 0 -> {
                tools.addView(withRuler(text));
                tools.addView(app.button("文字排版", this::layoutSettings));
            }
            case 1 -> {
                var thumbnail = new android.widget.ImageView(app);
                thumbnail.setScaleType(android.widget.ImageView.ScaleType.FIT_CENTER);
                thumbnail.setImageBitmap(image);
                thumbnail.setContentDescription(image == null ? "尚未选择图片" : "原图");
                thumbnail.setMinimumHeight(app.dp(140));
                thumbnail.setMaxHeight(app.dp(190));
                thumbnail.setAdjustViewBounds(true);
                tools.addView(withRuler(thumbnail));
                tools.addView(app.actionRow(app.button(image == null ? "选择图片" : "更换图片", () -> app.pickImage(false)),
                        app.button("图片排版", this::layoutSettings)));
            }
            case 2 -> {
                tools.addView(toolbar(app.button("添加对象", this::addMenu),
                        app.button("纸张", this::paperSettings), app.button("图层", this::chooseLayer)));
                var canvasSize = new LinearLayout.LayoutParams(-1, app.dp(360));
                canvasSize.setMargins(0, app.dp(8), 0, app.dp(8));
                tools.addView(paper, canvasSize);
                lengthLabel = app.label("", 13);
                editButton = app.button("属性", this::editLayer);
                duplicateButton = app.button("复制", this::duplicateLayer);
                tools.addView(toolbar(editButton, duplicateButton, app.button("适合纸张", paper::resetViewport)));
                tools.addView(app.label("单指拖对象或圆点，双指平移或缩放纸面。", 13));
                selectionChanged();
            }
            case 3 -> tools.addView(app.button("导入 raster1 JSON 或 RAW", app::openDocument));
            case 4 -> {
                tools.addView(app.button(drawing == null ? "全屏绘制" : "继续全屏绘制",
                        () -> DrawingEditor.open(app, drawing, bytes -> {
                    drawing = bytes;
                    showMode();
                    changed();
                })));
                if (drawing != null) {
                    tools.addView(app.actionRow(app.button("存为素材", () -> app.saveMaterial(drawing)),
                            app.button("导出 JSON", () -> app.exportRaster(drawing, 9))));
                    tools.addView(app.button("插入当前排版", () -> {
                        selectMode(2);
                        add(PaperComposition.PHOTO, Raster.bitmap(drawing, Color.TRANSPARENT));
                    }));
                }
                tools.addView(app.button("本地素材库", () -> app.chooseMaterial(bytes -> {
                    drawing = bytes;
                    showMode();
                    changed();
                })));
            }
            default -> { }
        }
        if (lengthLabel != null) {
            tools.addView(lengthLabel);
            updatePaperLength(printedRows);
        }
        if (mode < 3) {
            tools.addView(app.actionRow(app.button("黑白转换", this::toneSettings),
                    app.button("预设", app::presets)));
        }
        tools.addView(app.button("查看打印预览", () -> {
            if (mode != 3) render(true);
            app.jumpToPreview();
        }));
    }

    private LinearLayout withRuler(View editor) {
        var row = new LinearLayout(app);
        row.addView(editor, new LinearLayout.LayoutParams(0, -2, 1));
        lengthGauge = new View(app) {
            private final PaperRuler ruler = new PaperRuler(app);

            @Override protected void onDraw(Canvas canvas) {
                if (printedRows == 0) return;
                int inset = app.dp(14);
                float scale = (getHeight() - 2f * inset) / printedRows;
                ruler.draw(canvas, app.dp(13), inset, scale, printedRows, getHeight(), printedRows);
            }
        };
        row.addView(lengthGauge, new LinearLayout.LayoutParams(app.dp(43), -1));
        lengthLabel = app.label("", 13);
        return row;
    }

    void updatePaperLength(int rows) {
        printedRows = rows;
        if (lengthLabel != null) {
            String value = rows > 0
                    ? String.format(java.util.Locale.getDefault(), "纸长约 %.1f mm · %d 点 · 300 DPI", rows / PaperRuler.DOTS_PER_MM, rows)
                    : "纸长 · 等待可打印内容";
            lengthLabel.setText(value);
            if (lengthGauge != null) lengthGauge.invalidate();
        }
        paper.invalidate();
    }

    private void changed() {
        app.invalidatePreview();
        main.removeCallbacks(renderLater);
        main.postDelayed(renderLater, 250);
    }

    void cancelPendingPreview() {
        main.removeCallbacks(renderLater);
    }

    void setRaster(String name, byte[] bytes) {
        importedName = name;
        imported = bytes;
        render(false);
    }

    PrintDraft draft(PrintOptions print) {
        return new PrintDraft(mode, preset(print), text.getText().toString(), image,
                mode == 2 ? composition.snapshot() : null);
    }

    void restore(PrintDraft draft, byte[] bytes) {
        if (draft == null || draft.mode() == 3) {
            composition.layers.clear();
            composition.height = bytes.length / Raster.STRIDE;
            composition.autoLength = false;
            var layer = new PaperComposition.Layer(PaperComposition.PHOTO, 0, Raster.preview(bytes));
            layer.bounds.set(0, 0, Raster.WIDTH, composition.height);
            composition.layers.add(layer);
            tone = PaperRenderer.Tone.DEFAULT;
            selected = layer;
            paper.resetViewport();
            selectMode(2);
            selectionChanged();
            changed();
            return;
        }
        layout = draft.preset().layout();
        tone = draft.preset().tone();
        switch (draft.mode()) {
            case 0 -> text.setText(draft.text());
            case 1 -> image = draft.image();
            case 2 -> {
                composition.height = draft.paper().height;
                composition.autoLength = draft.paper().autoLength;
                composition.layers.clear();
                composition.layers.addAll(draft.paper().layers);
                selected = null;
                paper.resetViewport();
            }
            case 4 -> drawing = bytes;
            default -> throw new IllegalArgumentException("无法恢复任务");
        }
        selectMode(draft.mode());
        showMode();
        changed();
    }

    void insertIllustration(byte[] bytes) {
        selectMode(2);
        add(PaperComposition.PHOTO, Raster.bitmap(bytes, Color.TRANSPARENT));
    }

    private void render(boolean explicit) {
        main.removeCallbacks(renderLater);
        PaperRenderer.LayoutOptions settings = layout;
        PaperRenderer.Tone conversion = tone;
        switch (mode) {
            case 0 -> {
                String value = text.getText().toString();
                if (!explicit && value.isBlank()) return;
                app.generate("文字", () -> PaperRenderer.text(value, settings, conversion));
            }
            case 1 -> {
                Bitmap source = image;
                if (source == null) {
                    if (explicit) app.show("请先选择图片");
                }
                else app.generate("图片", () -> PaperRenderer.image(source, settings, conversion));
            }
            case 2 -> {
                if (!explicit && composition.layers.isEmpty()) return;
                PaperComposition snapshot = composition.snapshot();
                app.generate("高级排版", () -> snapshot.raster(conversion));
            }
            case 3 -> {
                byte[] bytes = imported;
                if (bytes != null) app.generate(importedName, () -> bytes);
            }
            case 4 -> {
                byte[] bytes = drawing;
                if (bytes != null) app.generate("手绘纸条", () -> bytes);
            }
            default -> { }
        }
    }

    void setImage(Bitmap bitmap, boolean layer) {
        if (layer) add(PaperComposition.PHOTO, bitmap);
        else {
            image = bitmap;
            if (mode == 1) showMode();
            changed();
        }
        app.show("已载入图片");
    }

    private void add(int kind, Bitmap bitmap) {
        if (composition.layers.size() >= 24) {
            app.show("每张纸最多 24 个图层");
            return;
        }
        int height = kind == PaperComposition.QR ? 220 : kind == PaperComposition.PHOTO ? 180 : 80;
        int top = Math.max(0, Math.min(composition.height - height, 24 + (composition.layers.size() % 8) * 16));
        var layer = new PaperComposition.Layer(kind, top, bitmap);
        layer.bounds.bottom = Math.min(layer.bounds.bottom, composition.height);
        editLayer(layer, true);
    }

    private LinearLayout toolbar(View... controls) {
        LinearLayout row = new LinearLayout(app);
        for (View control : controls) {
            var params = new LinearLayout.LayoutParams(0, -2, 1);
            params.setMargins(app.dp(2), 0, app.dp(2), 0);
            row.addView(control, params);
        }
        return row;
    }

    private void addMenu() {
        new StudioDialog(app).setTitle("添加到纸面")
                .setItems(new String[]{"文字框", "标签", "图片", "二维码", "本地手绘素材"}, (dialog, item) -> {
                    if (item == PaperComposition.PHOTO) app.pickImage(true);
                    else if (item == 4) app.chooseMaterial(bytes -> add(PaperComposition.PHOTO,
                            Raster.bitmap(bytes, Color.TRANSPARENT)));
                    else add(item, null);
                }).show();
    }

    private void paperSettings() {
        LinearLayout fields = app.column();
        CheckBox auto = check("自动截断尾部空白", composition.autoLength);
        fields.addView(auto);
        EditText height = field(fields, "固定纸长（点）", Math.max(120, composition.height));
        height.setEnabled(!auto.isChecked());
        auto.setOnCheckedChangeListener((button, checked) -> height.setEnabled(!checked));
        CheckBox guides = check("吸附到纸张中心线", snap);
        fields.addView(guides);
        form("纸张", fields, () -> {
            int rows = auto.isChecked() ? Raster.MAX_HEIGHT : number(height, 120, 1400);
            if (composition.layers.stream().anyMatch(layer -> layer.bounds.bottom > rows)) {
                height.setError("请先把图层移到新纸长范围内");
                throw new IllegalArgumentException("纸张长度不足");
            }
            if (rows != composition.height || composition.autoLength != auto.isChecked()) {
                composition.height = rows;
                composition.autoLength = auto.isChecked();
                paper.resetViewport();
                changed();
            }
            snap = guides.isChecked();
        });
    }

    private void duplicateLayer() {
        if (selected == null) return;
        if (composition.layers.size() >= 24) {
            app.show("每张纸最多 24 个图层");
            return;
        }
        var copy = selected.snapshot();
        copy.bounds.offsetTo(Math.min(copy.bounds.left + 16, Raster.WIDTH - copy.bounds.width()),
                Math.min(copy.bounds.top + 16, composition.height - copy.bounds.height()));
        composition.layers.add(copy);
        selected = copy;
        selectionChanged();
        changed();
    }

    private void selectionChanged() {
        if (editButton != null) editButton.setEnabled(selected != null);
        if (duplicateButton != null) duplicateButton.setEnabled(selected != null);
        paper.invalidate();
    }

    private void chooseLayer() {
        if (composition.layers.isEmpty()) {
            app.show("纸面还没有图层");
            return;
        }
        String[] names = composition.layers.stream().map(layer -> switch (layer.kind) {
            case PaperComposition.PHOTO -> "图片";
            case PaperComposition.QR -> "二维码 · " + layer.text;
            default -> layer.text.toString();
        }).toArray(String[]::new);
        new StudioDialog(app).setTitle("图层（底层到顶层）").setItems(names, (dialog, index) -> {
            selected = composition.layers.get(index);
            selectionChanged();
            editLayer();
        }).show();
    }

    private void editLayer() {
        PaperComposition.Layer layer = selected;
        if (layer == null) {
            app.show("先点按画布上的对象，或从图层列表选择");
            return;
        }
        editLayer(layer, false);
    }

    private void editLayer(PaperComposition.Layer layer, boolean inserting) {
        LinearLayout form = app.column();
        EditText x = field(form, "X", (int) layer.bounds.left);
        EditText y = field(form, "Y", (int) layer.bounds.top);
        EditText width = field(form, "宽", (int) layer.bounds.width());
        EditText height = field(form, "高", (int) layer.bounds.height());
        EditText content = new EditText(app);
        content.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_MULTI_LINE);
        content.setText(layer.text);
        if (layer.kind != PaperComposition.PHOTO) form.addView(content);
        Spinner alignment = choice("居左", "居中", "居右");
        alignment.setSelection(layer.style.align());
        Spinner formats = choice("纯文字", "网页链接", "电子邮件");
        formats.setSelection(layer.qr.format());
        Spinner correction = choice("L", "M", "Q", "H");
        correction.setSelection(java.util.List.of("L", "M", "Q", "H").indexOf(layer.qr.correction().name()));
        Spinner shape = choice("方形", "圆角", "圆点");
        shape.setSelection(layer.qr.shape());
        CheckBox border = check("显示外框", layer.qr.border());
        if (layer.kind == PaperComposition.QR) {
            form.addView(formats);
            form.addView(correction);
            form.addView(shape);
            form.addView(border);
        } else if (layer.kind != PaperComposition.PHOTO) {
            form.addView(alignment);
            form.addView(app.button("设置所选文字的字体与样式", () -> spanStyle(content)));
        }
        Spinner order = choice("保留层级", "移到顶层", "移到底层", "向上一层", "向下一层");
        if (!inserting) form.addView(order);
        Dialog editor = form(inserting ? "插入图层" : "图层属性", form, () -> {
            int left = number(x, 0, 575);
            int top = number(y, 0, composition.height - 1);
            int w = number(width, 12, Raster.WIDTH - left);
            int h = number(height, 12, composition.height - top);
            if (content.length() > 10000) throw new IllegalArgumentException("文字最多 10000 字符");
            var qr = new QrMark(formats.getSelectedItemPosition(),
                    ErrorCorrectionLevel.valueOf(correction.getSelectedItem().toString()),
                    shape.getSelectedItemPosition(), border.isChecked());
            if (layer.kind == PaperComposition.QR) {
                try {
                    qr.validate(content.getText().toString(), Math.min(w, h));
                } catch (IllegalArgumentException error) {
                    content.setError(error.getMessage());
                    throw error;
                }
            }
            layer.bounds.set(left, top, left + w, top + h);
            layer.text = new SpannableString(content.getText());
            layer.style = new PaperRenderer.LayoutOptions(0, 0, w, alignment.getSelectedItemPosition(),
                    30, 4, "sans-serif", false, 0, 0, 400);
            layer.qr = qr;
            if (inserting) composition.layers.add(layer);
            else reorder(layer, order.getSelectedItemPosition());
            selected = layer;
            changed();
            selectionChanged();
        });
        if (!inserting) form.addView(app.button("删除图层", () ->
                new StudioDialog(app).setTitle("删除所选图层？")
                        .setNegativeButton("取消", null).setPositiveButton("删除", (dialog, which) -> {
                            composition.layers.remove(layer);
                            changed();
                            selected = null;
                            editor.dismiss();
                            selectionChanged();
                        }).show()));
    }

    private void reorder(PaperComposition.Layer layer, int operation) {
        int current = composition.layers.indexOf(layer);
        int last = composition.layers.size() - 1;
        int destination = switch (operation) {
            case 1 -> last;
            case 2 -> 0;
            case 3 -> Math.min(last, current + 1);
            case 4 -> Math.max(0, current - 1);
            default -> current;
        };
        if (destination == current) return;
        composition.layers.remove(current);
        composition.layers.add(destination, layer);
    }

    private void spanStyle(EditText content) {
        int start = Math.min(content.getSelectionStart(), content.getSelectionEnd());
        int end = Math.max(content.getSelectionStart(), content.getSelectionEnd());
        if (start < 0 || end <= start) {
            content.setError("请先选中文字");
            return;
        }
        LinearLayout form = app.column();
        EditText size = field(form, "字号（点）", 30);
        Spinner family = choice("sans-serif", "serif", "monospace", "sans-serif-condensed");
        CheckBox bold = check("粗体", false);
        CheckBox italic = check("斜体", false);
        form.addView(family);
        form.addView(bold);
        form.addView(italic);
        form("字体与样式", form, () -> {
            int value = number(size, 8, 96);
            var editable = content.getText();
            replaceSpan(editable, new TypefaceSpan(family.getSelectedItem().toString()), start, end);
            replaceSpan(editable, new AbsoluteSizeSpan(value), start, end);
            int style = (bold.isChecked() ? 1 : 0) | (italic.isChecked() ? 2 : 0);
            replaceSpan(editable, new StyleSpan(style), start, end);
        });
    }

    private static void replaceSpan(android.text.Editable text, android.text.style.CharacterStyle style, int start, int end) {
        for (var span : text.getSpans(start, end, style.getClass())) {
            int from = text.getSpanStart(span);
            int to = text.getSpanEnd(span);
            text.removeSpan(span);
            if (from < start) text.setSpan(span, from, start, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
            if (to > end) {
                android.text.style.CharacterStyle tail;
                if (span instanceof TypefaceSpan typeface) tail = new TypefaceSpan(typeface.getFamily());
                else if (span instanceof AbsoluteSizeSpan size) tail = new AbsoluteSizeSpan(size.getSize());
                else tail = new StyleSpan(((StyleSpan) span).getStyle());
                text.setSpan(tail, end, to, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
            }
        }
        text.setSpan(style, start, end, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
    }

    private void layoutSettings() {
        LinearLayout form = app.column();
        EditText mx = field(form, "左右边距", layout.marginX());
        EditText my = field(form, "上下边距", layout.marginY());
        EditText width = field(form, "内容宽度", layout.width());
        Spinner align = choice("居左", "居中", "居右");
        align.setSelection(layout.align());
        form.addView(align);
        boolean photo = mode == 1;
        EditText size = photo ? null : field(form, "字号（点）", layout.fontSize());
        EditText spacing = photo ? null : field(form, "行距（点）", layout.spacing());
        EditText family = new EditText(app);
        family.setText(layout.family());
        CheckBox bold = check("粗体", layout.bold());
        Spinner rotation = choice("0°", "90°", "180°", "270°");
        Spinner fit = choice("适配宽度", "保持像素", "裁切填满");
        rotation.setSelection(layout.rotation() / 90);
        fit.setSelection(layout.fit());
        EditText crop = null;
        if (photo) {
            form.addView(rotation);
            form.addView(fit);
            crop = field(form, "裁切高度", layout.cropHeight());
        } else {
            form.addView(app.label("字体族（如 sans-serif、serif、monospace）", 14));
            form.addView(family);
            form.addView(bold);
        }
        EditText cropHeight = crop;
        form(photo ? "图片排版" : "文字排版", form, () -> {
            int margin = number(mx, 0, 120);
            layout = new PaperRenderer.LayoutOptions(margin, number(my, 0, 200), number(width, 48, 576 - 2 * margin),
                    align.getSelectedItemPosition(), photo ? layout.fontSize() : number(size, 8, 96),
                    photo ? layout.spacing() : number(spacing, 0, 40), family.getText().toString(), bold.isChecked(),
                    rotation.getSelectedItemPosition() * 90, fit.getSelectedItemPosition(),
                    photo ? number(cropHeight, 24, 1400) : layout.cropHeight());
            changed();
        });
    }

    private void toneSettings() {
        LinearLayout form = app.column();
        Spinner method = choice("阈值", "Floyd–Steinberg", "有序抖动");
        method.setSelection(tone.method());
        form.addView(method);
        EditText threshold = field(form, "阈值", tone.threshold());
        EditText brightness = field(form, "亮度", tone.brightness());
        EditText contrast = field(form, "对比度（百分比）", (int) Math.round(tone.contrast() * 100));
        EditText gamma = field(form, "Gamma（百分比）", (int) Math.round(tone.gamma() * 100));
        CheckBox invert = check("反相", tone.invert());
        form.addView(invert);
        form("黑白转换", form, () -> {
            tone = new PaperRenderer.Tone(method.getSelectedItemPosition(),
                number(threshold, 0, 255), number(brightness, -100, 100), number(contrast, 20, 300) / 100d,
                number(gamma, 20, 300) / 100d, invert.isChecked());
            changed();
        });
    }

    Spinner choice(String... values) {
        Spinner spinner = new Spinner(app, Spinner.MODE_DROPDOWN);
        spinner.setBackground(app.shape(app.getColor(android.R.color.system_neutral1_100), 14));
        spinner.setPadding(app.dp(10), 0, app.dp(10), 0);
        spinner.setPopupBackgroundDrawable(app.shape(app.getColor(android.R.color.system_neutral1_50), 16));
        ArrayAdapter<String> adapter = new ArrayAdapter<>(app, android.R.layout.simple_spinner_dropdown_item, values);
        adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        spinner.setAdapter(adapter);
        spinner.setTooltipText("点按选择");
        spinner.setMinimumHeight(app.dp(48));
        return spinner;
    }

    CheckBox check(String title, boolean checked) {
        CheckBox check = new CheckBox(app);
        check.setText(title);
        check.setChecked(checked);
        return check;
    }

    EditText field(LinearLayout form, String name, int initial) {
        form.addView(app.label(name, 14));
        EditText field = new EditText(app);
        field.setContentDescription(name);
        field.setInputType(InputType.TYPE_CLASS_NUMBER | InputType.TYPE_NUMBER_FLAG_SIGNED);
        field.setText(Integer.toString(initial));
        form.addView(field);
        return field;
    }

    int number(EditText field, int min, int max) {
        try {
            int value = Integer.parseInt(field.getText().toString().strip());
            if (value >= min && value <= max) return value;
        } catch (NumberFormatException ignored) {
            // Present a field-specific range below.
        }
        field.setError("请输入 " + min + "–" + max + " 的整数");
        field.requestFocus();
        throw new IllegalArgumentException("请检查输入范围");
    }

    Dialog form(String title, LinearLayout fields, Runnable save) {
        fields.setPadding(app.dp(24), app.dp(8), app.dp(24), app.dp(12));
        ScrollView scroll = new ScrollView(app);
        scroll.addView(fields);
        var errorMessage = app.label("", 14);
        errorMessage.setVisibility(View.GONE);
        errorMessage.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        fields.addView(errorMessage, 0);
        return new StudioDialog(app).setTitle(title).setView(scroll).setNegativeButton("取消", null)
                .keepOpenOnApply().setPositiveButton("应用", (dialog, which) -> {
                    try {
                        save.run();
                        dialog.dismiss();
                    } catch (IllegalArgumentException error) {
                        errorMessage.setText(error.getMessage());
                        errorMessage.setVisibility(View.VISIBLE);
                        if (fields.findFocus() == null) scroll.smoothScrollTo(0, 0);
                    }
                }).show();
    }

    private final class PaperView extends View {
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint backdrop = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final PaperRuler ruler = new PaperRuler(app);
        private PaperGesture gesture;
        private float zoom = 1, offsetX, offsetY;
        private float downX, downY, lastX, lastY;
        private int corner = -1;
        private boolean moved, multiTouch, edited;
        private PaperPlacement original;

        PaperView() {
            super(app);
            setContentDescription("排版画布");
            setBackground(app.shape(app.getColor(android.R.color.system_neutral1_100), 18));
            setClipToOutline(true);
        }

        private float viewportWidth() { return Math.max(1, getWidth() - app.dp(43)); }

        private float scale() {
            float margin = app.dp(14) * 2f;
            return zoom * Math.max(0, Math.min((viewportWidth() - margin) / Raster.WIDTH,
                    (getHeight() - margin) / composition.height));
        }

        private float left() { return (viewportWidth() - Raster.WIDTH * scale()) / 2 + offsetX; }
        private float top() { return (getHeight() - composition.height * scale()) / 2 + offsetY; }

        private void constrainViewport() {
            float horizontal = Math.max(0, (Raster.WIDTH * scale() - viewportWidth()) / 2);
            float vertical = Math.max(0, (composition.height * scale() - getHeight()) / 2);
            offsetX = PaperPlacement.clamp(offsetX, -horizontal, horizontal);
            offsetY = PaperPlacement.clamp(offsetY, -vertical, vertical);
        }

        void resetViewport() {
            zoom = 1;
            offsetX = offsetY = 0;
            invalidate();
        }

        @Override protected void onSizeChanged(int w, int h, int oldw, int oldh) {
            constrainViewport();
        }

        @Override protected void onDraw(Canvas canvas) {
            backdrop.setColor(app.getColor(android.R.color.system_neutral2_200));
            int step = app.dp(14);
            for (int y = step / 2; y < getHeight(); y += step) {
                for (int x = step / 2; x < getWidth(); x += step) {
                    canvas.drawCircle(x, y, app.dp(1), backdrop);
                }
            }
            canvas.save();
            canvas.clipRect(0, 0, viewportWidth(), getHeight());
            canvas.translate(left(), top());
            canvas.scale(scale(), scale());
            canvas.clipRect(0, 0, Raster.WIDTH, composition.height);
            composition.draw(canvas, false);
            canvas.restore();
            backdrop.setColor(app.getColor(android.R.color.system_neutral1_100));
            canvas.drawRect(viewportWidth(), 0, getWidth(), getHeight(), backdrop);
            ruler.draw(canvas, viewportWidth() + app.dp(8), top(), scale(), composition.height,
                    getHeight(), printedRows);
            if (selected == null) return;
            canvas.save();
            canvas.clipRect(0, 0, viewportWidth(), getHeight());
            var box = selected.bounds;
            float l = left() + box.left * scale(), r = left() + box.right * scale();
            float t = top() + box.top * scale(), b = top() + box.bottom * scale();
            paint.setColor(app.getColor(android.R.color.system_accent1_700));
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeWidth(app.dp(2));
            canvas.drawRect(l, t, r, b, paint);
            if (snap && edited && corner < 0) {
                if (Math.abs(box.centerX() - Raster.WIDTH / 2f) < .01f) {
                    canvas.drawLine(left() + Raster.WIDTH * scale() / 2, top(),
                            left() + Raster.WIDTH * scale() / 2, top() + composition.height * scale(), paint);
                }
                if (Math.abs(box.centerY() - composition.height / 2f) < .01f) {
                    canvas.drawLine(left(), top() + composition.height * scale() / 2,
                            left() + Raster.WIDTH * scale(), top() + composition.height * scale() / 2, paint);
                }
            }
            paint.setStyle(Paint.Style.FILL);
            for (int i = 0; i < 4; i++) {
                canvas.drawCircle((i & 1) == 0 ? l : r, (i & 2) == 0 ? t : b, app.dp(6), paint);
            }
            canvas.restore();
        }

        private int handleAt(float x, float y) {
            if (selected == null) return -1;
            float closest = app.dp(22);
            int result = -1;
            for (int i = 0; i < 4; i++) {
                var b = selected.bounds;
                float cx = left() + ((i & 1) == 0 ? b.left : b.right) * scale();
                float cy = top() + ((i & 2) == 0 ? b.top : b.bottom) * scale();
                float distance = (float) Math.hypot(x - cx, y - cy);
                if (distance < closest) {
                    closest = distance;
                    result = i;
                }
            }
            return result;
        }

        @Override public boolean onTouchEvent(MotionEvent event) {
            if (scale() <= 0) return false;
            int action = event.getActionMasked();

            if (action == MotionEvent.ACTION_DOWN) {
                if (event.getX() >= viewportWidth()) return false;
                gesture = null;
                downX = lastX = event.getX();
                downY = lastY = event.getY();
                moved = multiTouch = edited = false;
                corner = handleAt(downX, downY);
                if (corner < 0) {
                    selected = null;
                    for (int i = composition.layers.size() - 1; i >= 0; i--) {
                        var layer = composition.layers.get(i);
                        if (layer.bounds.contains((downX - left()) / scale(), (downY - top()) / scale())) {
                            selected = layer;
                            break;
                        }
                    }
                }
                original = selected == null ? null : new PaperPlacement(selected.bounds.left, selected.bounds.top,
                        selected.bounds.width(), selected.bounds.height());
                getParent().requestDisallowInterceptTouchEvent(original != null || zoom > 1);
                selectionChanged();
            } else if (action == MotionEvent.ACTION_POINTER_DOWN) {
                multiTouch = true;
                gesture = event.getPointerCount() == 2 ? gesture(event) : null;
                getParent().requestDisallowInterceptTouchEvent(true);
                if (edited) {
                    place(original);
                    edited = false;
                    changed();
                }
            } else if (action == MotionEvent.ACTION_POINTER_UP) {
                gesture = null;
            } else if (action == MotionEvent.ACTION_MOVE && multiTouch && event.getPointerCount() == 2) {
                PaperGesture current = gesture(event);
                if (gesture != null) {
                    float x = (gesture.x() - left()) / scale();
                    float y = (gesture.y() - top()) / scale();
                    zoom = current.zoomFrom(gesture, zoom, app.dp(12));
                    offsetX += current.x() - left() - x * scale();
                    offsetY += current.y() - top() - y * scale();
                    constrainViewport();
                    invalidate();
                }
                gesture = current;
            } else if (action == MotionEvent.ACTION_MOVE && !multiTouch) {
                float x = event.getX(), y = event.getY();
                moved |= Math.hypot(x - downX, y - downY) > android.view.ViewConfiguration.get(app).getScaledTouchSlop();
                if (moved && original != null) {
                    if (!edited) {
                        cancelPendingPreview();
                        app.invalidatePreview();
                        edited = true;
                    }
                    place(original.drag((x - downX) / scale(), (y - downY) / scale(), corner,
                            Raster.WIDTH, composition.height, snap, app.dp(7) / scale()));
                } else if (moved && zoom > 1) {
                    offsetX += x - lastX;
                    offsetY += y - lastY;
                    constrainViewport();
                    invalidate();
                }
                lastX = x;
                lastY = y;
            } else if (action == MotionEvent.ACTION_UP || action == MotionEvent.ACTION_CANCEL) {
                if (edited) {
                    if (action == MotionEvent.ACTION_CANCEL) place(original);
                    changed();
                } else if (!moved && !multiTouch && action == MotionEvent.ACTION_UP) performClick();
                edited = false;
                getParent().requestDisallowInterceptTouchEvent(false);
                invalidate();
            }
            return true;
        }

        private PaperGesture gesture(MotionEvent event) {
            return PaperGesture.between(event.getX(0), event.getY(0), event.getX(1), event.getY(1));
        }

        private void place(PaperPlacement placement) {
            selected.bounds.set(placement.x(), placement.y(), placement.x() + placement.width(), placement.y() + placement.height());
            invalidate();
        }

        @Override public boolean performClick() {
            super.performClick();
            return true;
        }
    }
}
