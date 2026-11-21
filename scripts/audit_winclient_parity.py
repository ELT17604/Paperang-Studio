import argparse
import collections
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "doc/MAC-WINCLIENT-CONTROLS.zh-CN.md"
KINDS = (
    "Button", "Toggle", "Picker", "TextField", "SecureField", "TextEditor",
    "Slider", "Stepper", "Menu", "GroupBox", "TableColumn", "ProgressView",
    "SystemFontPicker", "List", "Text", "Label", "integer", "slider",
)
CONTROL = re.compile(r"\b(" + "|".join(KINDS) + r")\s*(?:\(|(?=\s*\{))")
VIEW = re.compile(r"\bvar\s+(\w+)\s*:\s*some\s+View")
EQUIVALENTS = {
    "导入预设": "WorkspaceEditing.cs / PresetFile",
    "保存预设": "WorkspaceEditing.cs / PresetFile",
    "快捷预设": "WorkspaceDesk.cs / quickPreset",
    "内容宽度 px": "WorkspaceEditing.cs / contentWidth",
    "左右最小边距 px": "WorkspaceWindow.cs / margin",
    "上下边距 px": "WorkspaceWindow.cs / marginY",
    "粗体": "WorkspaceWindow.cs / bold",
    "行间距": "WorkspaceWindow.cs / spacing",
    "字号": "WorkspaceWindow.cs / font",
    "缩放方式": "WorkspaceEditing.cs / imageFit",
    "旋转": "WorkspaceEditing.cs / rotation",
    "裁切高度 px": "WorkspaceEditing.cs / cropHeight",
    "对齐": "WorkspaceWindow.cs / alignment",
    "黑白方式": "WorkspaceEditing.cs / conversion",
    "黑白阈值": "WorkspaceWindow.cs / threshold",
    "亮度": "WorkspaceEditing.cs / brightness",
    "对比度": "WorkspaceEditing.cs / contrast",
    "Gamma": "WorkspaceEditing.cs / gamma",
    "黑白反相": "WorkspaceEditing.cs / invert",
    "打印浓度": "WorkspaceWindow.cs / density",
    "份数": "WorkspaceWindow.cs / copies",
    "打印前送纸 行": "WorkspaceWindow.cs / before",
    "打印后送纸 行": "WorkspaceWindow.cs / feed",
    "每份后都送纸": "WorkspaceWindow.cs / between",
    "导出PNG": "WorkspaceWindow.cs / Export",
    "导出位图数据": "WorkspaceWindow.cs / Export",
    "导入位图…": "WorkspaceWindow.cs / Import",
    "选择图片…": "WorkspaceWindow.cs / Import",
    "移到最上层": "WorkspaceDesk.cs / BuildFreeLayout",
    "连接中转": "WorkspaceWindow.cs / BuildRelay",
    "所有者 JSON 文件路径": "ConnectionPage.cs / owner",
    "选择文件…": "ConnectionPage.cs / owner",
}
PARTIAL = {
    "检查 GitHub 更新…": "PreferencesPage.cs：有发布页入口，尚缺 WinClient 发行版本比较",
    "恢复后台自动打印": "Program.cs：托盘目前提供工作台与退出；自动打印开关在远程页面",
    "复制本地 API 令牌": "LocalApiPage.cs：桌面页面可复制令牌；托盘无对应快捷项",
}
EQUIVALENTS.update({
    "高级排版…": "WorkspaceDesk.cs / BuildFreeLayout + LayerEditing.cs：可编辑文字、标签、QR、图片；确认/取消草稿",
    "插入二维码…": "WorkspaceFiles.cs / Qr：内容格式、纠错、模块形态与外框；18 种组合经独立 ZXing 解码",
    "字体与样式…": "LayerEditing.cs：原生 RichEditBox 选区字体、字号、粗斜体与下划线",
    "连接打印机": "PrinterDiscovery.cs / ConnectPrinterSelection + ClientHost.cs：选择、连接、取消、结果状态",
    "刷新状态": "WorkspaceWindow.cs / BuildPrinter + RefreshSession",
    "刷新电量与纸仓": "WorkspaceWindow.cs / BuildPrinter：独立刷新与实时状态",
    "断开连接": "WorkspaceWindow.cs / BuildPrinter + ClientHost.DisconnectAsync",
    "只应用浓度并回读": "WorkspaceWindow.cs / BuildPrinter + IPrinter.ApplyDensityAsync",
    "读取全部已知GET": "EngineeringPage.cs / BuildEngineering",
    "导出会话JSON": "EngineeringPage.cs：字段、任务、传输参数与收发帧记录",
    "只读目录": "EngineeringPage.cs：37 条命令与证据说明，GET 白名单独立限制",
    "发送GET查询": "EngineeringPage.cs / ReadDevice",
    "解锁工程参数写入（每次发送前仍确认）": "EngineeringPage.cs + P2Engineering.cs：相同 8 类写命令及载荷限制",
    "发送帧（只发一次）": "EngineeringPage.cs + ClientHost.SendEngineeringAsync",
    "图像载荷 bytes（72的倍数）": "WorkspaceEditing.cs / transferFrame + P2Session/UsbP2Session",
    "BLE分段上限 bytes": "WorkspaceEditing.cs / transferChunk + P2Session",
    "BLE写入间隔 ms": "WorkspaceEditing.cs / transferWrite + P2Session",
    "图像帧间隔 ms": "WorkspaceEditing.cs / transferPause",
    "单命令超时 秒": "WorkspaceEditing.cs / transferTimeout",
    "每份数据后等待 秒": "WorkspaceEditing.cs / transferFinish",
    "灰度": "LayerEditing.cs：保留图片灰度源，独立对象阈值、抖动、亮度、对比度、Gamma、反相",
    "打印选中任务": "RelayQueuePage.cs + RelayEngine.RequestPrint：持久化认领后再取正文、发送及回报",
    "拒绝": "RelayQueuePage.cs：仅待处理任务，发送前确认并刷新",
    "复制到工作台": "RelayQueuePage.cs：C 权限读取完整内容后按真实模板合成点阵",
    "生成预览": "RelayQueuePage.cs：拒绝 truncated 正文",
    "保存并应用": "TemplatePage.cs：验证草稿后写入加密配置",
    "C 密钥或完整口令": "AdministrationPage.cs：独立短期 C 管理会话，不替换接收凭证",
    "锁定": "AdministrationPage.cs / LockAdministration：清除凭证、详情、预览及会话",
    "额度设置": "AdministrationTools.cs：身份/匿名/认证配额与空白继承",
})
PAGE_TARGETS = {
    "APIRoutes.swift": "LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs",
    "PaperEditor.swift": "LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs",
    "PaperTTY.swift": "TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY）",
    "RelayLayout.swift": "TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs",
    "RelaySecurityManager.swift": "AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs",
    "RelayView.swift": "ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs",
    "TrayMode.swift": "Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口）",
}
APP_VIEWS = {
    "App": "PreferencesPage.cs / WorkspaceWindow.cs",
    "body": "WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs",
    "header": "WorkspaceDesk.cs / ClientHost.cs",
    "workspace": "WorkspaceDesk.cs / WorkspaceEditing.cs",
    "input": "WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs",
    "layout": "WorkspaceEditing.cs / WorkspaceDesk.cs",
    "imageProcessing": "WorkspaceEditing.cs / WorkspaceImage.cs",
    "output": "WorkspaceWindow.cs / LocalPrintOptions.cs",
    "footer": "WorkspaceDesk.cs / WorkspaceWindow.cs",
    "devices": "PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择）",
    "history": "WorkspaceLibrary.cs / WorkspaceWindow.cs",
    "engineering": "EngineeringPage.cs",
    "fields": "EngineeringPage.cs / P2Queries.cs",
    "catalog": "EngineeringPage.cs / P2Command.cs",
    "raw": "EngineeringPage.cs / P2Engineering.cs",
    "transport": "WorkspaceEditing.cs / EngineeringPage.cs / P2Session.cs / UsbP2Session.cs",
    "logView": "EngineeringPage.cs / ProtocolTrace.cs",
}


def inventory():
    rows = []
    for path in sorted((ROOT / "Sources/PaperangLab").glob("*.swift")):
        lines = path.read_text(encoding="utf-8").splitlines()
        view = path.stem
        ordinal = 0
        for index, line in enumerate(lines):
            found_view = VIEW.search(line)
            if found_view:
                view = found_view.group(1)
            for control in CONTROL.finditer(line):
                ordinal += 1
                snippet = line[control.start():].strip()
                if snippet.rstrip().endswith("("):
                    snippet += " " + " ".join(part.strip() for part in lines[index + 1:index + 4])
                literal = re.search(r'"((?:[^"\\]|\\.)*)"', snippet)
                label = literal.group(1) if literal else snippet[:120]
                if label in PARTIAL:
                    state, target = "存在具体差异", PARTIAL[label]
                elif label in EQUIVALENTS:
                    state, target = "有对应实现，未逐控件验收", EQUIVALENTS[label]
                elif path.name in PAGE_TARGETS or path.name == "App.swift" and view in APP_VIEWS:
                    state = "模块已定位，逐项行为待核对"
                    target = PAGE_TARGETS.get(path.name, APP_VIEWS.get(view, ""))
                elif control.group(1) in ("Text", "Label", "GroupBox", "TableColumn", "ProgressView", "List"):
                    state, target = "展示/结构元素待核对", "需检查布局、数据、空态及状态更新"
                else:
                    state, target = "未确认对应，需补齐或逐项证明", "尚无本条控件的等价实现/验证记录"
                rows.append((f"{path.stem}-{ordinal:03}", path, index + 1, view, control.group(1), label, state, target))
    return rows


def clean(value):
    return str(value).replace("|", "／").replace("\n", " ").replace("`", "'")


def report():
    rows = inventory()
    counts = collections.Counter(row[6] for row in rows)
    text = [
        "# MacClient → WinClient 逐控件对照",
        "",
        "此表由源码生成，覆盖 PaperangLab 全部 Swift 文件中的控件声明、展示元素和自定义数值控件。",
        "行号用于定位，动态循环的每个运行时选项仍需进一步展开验证；不将编译通过或名称相似当作功能对齐。",
        "这是完整源码盘点和迁移跟踪表，不是已完成对等验收的声明。macOS 实机、Windows 真实交互和打印硬件需分别记录证据。",
        "Windows 页面位于 Windows/src/Paperang.WinClient；Core/Windows 库同名文件另行定位，*Checks.cs 位于 tests/windows/WinClientChecks。结构元素与动态循环尚不等于独立运行时控件。",
        "页面级实现、测试证据和剩余差异见 [WINCLIENT.md](WINCLIENT.md)。模块定位状态仅用于代码审查，不能计入已验证功能数量。",
        "",
        f"共 {len(rows)} 条声明；" + "；".join(f"{key}：{value}" for key, value in sorted(counts.items())) + "。",
        "",
        "## 控件和展示元素",
        "",
        "| 编号 | Mac 源码 | 页面/视图 | 类型 | 文案或表达式 | 状态 | Windows 对应或缺口 |",
        "|---|---|---|---|---|---|---|",
    ]
    for identifier, path, line, view, kind, label, state, target in rows:
        relative = path.relative_to(ROOT).as_posix()
        values = (identifier, f"[{path.name}:{line}](../{relative}#L{line})", view, kind, label, state, target)
        text.append("| " + " | ".join(clean(value) for value in values) + " |")
    return "\n".join(text) + "\n"


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    arguments = parser.parse_args()
    rendered = report()
    if arguments.check:
        if not OUTPUT.exists() or OUTPUT.read_text(encoding="utf-8") != rendered:
            raise SystemExit("Mac/WinClient 控件对照表需更新")
    else:
        OUTPUT.write_text(rendered, encoding="utf-8")
    print(f"PASS {len(inventory())} Mac UI declarations inventoried")
