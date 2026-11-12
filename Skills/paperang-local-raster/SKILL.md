---
name: paperang-local-raster
description: Create and validate import-ready raster1 monochrome bitmap resources for Paperang Studio P2 MacClient. Use when the user wants a printable note, text sheet, label, QR code, illustration, photo, or custom 576-dot layout as a local file; never submit a print job.
---

# Paperang 可打印位图资源

只生成本地文件，不连接打印机、不发起远程任务、不代替用户确认出纸。输出格式针对仓库中 MacClient 的「打印工作台 → 导入位图…」功能，而非 BLE 帧或 Relay API 请求。

## 制作流程

1. **选路径。** 纯文字使用 `scripts/render_text.py` 自动换行、分页；混排文字、照片、图形、二维码则用 Pillow/矢量渲染器等先画成**精确 576 像素宽**的 PNG，再调用 `scripts/pack_raster.py`。高度必须在 1–1400 像素之间；超长内容按语义分页，文件名按打印顺序编号。
2. **先排版后栅格化。** 白底黑字，预留合理边距（通常左右各 20–32 像素）。用实际支持目标语言的字体渲染中文字形。照片优先在生成设计 PNG 前完成灰度映射与抖动，避免默认阈值损失中间调；文字、条码、二维码使用清晰边缘。二维码至少留 4 个模块宽度的白色静区，最终位图扫码验证；不要靠抗锯齿缩小二维码模块。
3. **输出文件。** 对每张 PNG 打包为 `*.raster1.json`，同时生成从**打包字节反解**的 `*-preview.png`。不要手写位图 Base64 或 SHA-256，也不要自行转换为打印命令。
4. **验证与交付。** 对每份 JSON 调用 `scripts/verify_raster.py`；核对尺寸、行字节序、Base64、SHA-256，并检查实际解码预览是否被裁切、反相或出现不可识别字形。交付 JSON 和解码预览；如用户需要，附设计源 PNG 或 `.raw`。明确只有用户在客户端检查后手动点击打印才会出纸。

## 命令示例

相对于本 Skill 目录执行。需要 Python 3.10+、Pillow；文字示例要求指定本机已安装且覆盖目标字符的字体。

```sh
# UTF-8 文本 → 自动换行/分页的设计 PNG、raster1 JSON、解码预览
python scripts/render_text.py note.txt output/note --font /path/to/CJK-font.ttf --size 30

# 已经排好版的 576 像素宽 PNG → 打包 JSON、解码预览
python scripts/pack_raster.py design.png output/design.raster1.json --preview output/design-preview.png

# 独立复核格式与校验和；可选择导出客户端兼容的无头 .raw
python scripts/verify_raster.py output/design.raster1.json --preview output/checked.png --raw output/design.raw
```

- `pack_raster.py` **拒绝不合尺寸**的 PNG，不会偷偷拉伸或裁切。需要改变比例时，在设计阶段明确采用缩放、留白或裁切策略。
- `render_text.py` 支持 `--margin`、`--line-gap`、`--max-height` 和 `--threshold`；保留显式空行，超长文字按页编号。中文常用 macOS 系统字体可选 `/System/Library/Fonts/PingFang.ttc`，实际路径以本机为准。
- 参考 [raster1 字节规范](references/raster1.md) 处理其他语言/工具生成的结果。项目原始校验逻辑见 `Sources/PaperangLab/WorkspaceRaster.swift`；纸宽与位序见 `doc/PROTOCOL.zh-CN.md`。
