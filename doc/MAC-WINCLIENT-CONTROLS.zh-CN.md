# MacClient → WinClient 逐控件对照

此表由源码生成，覆盖 PaperangLab 全部 Swift 文件中的控件声明、展示元素和自定义数值控件。
行号用于定位，动态循环的每个运行时选项仍需进一步展开验证；不将编译通过或名称相似当作功能对齐。
这是完整源码盘点和迁移跟踪表，不是已完成对等验收的声明。macOS 实机、Windows 真实交互和打印硬件需分别记录证据。
Windows 页面位于 Windows/src/Paperang.WinClient；Core/Windows 库同名文件另行定位，*Checks.cs 位于 tests/windows/WinClientChecks。结构元素与动态循环尚不等于独立运行时控件。
页面级实现、测试证据和剩余差异见 [WINCLIENT.md](WINCLIENT.md)。模块定位状态仅用于代码审查，不能计入已验证功能数量。

共 455 条声明；存在具体差异：3；有对应实现，未逐控件验收：59；模块已定位，逐项行为待核对：393。

## 控件和展示元素

| 编号 | Mac 源码 | 页面/视图 | 类型 | 文案或表达式 | 状态 | Windows 对应或缺口 |
|---|---|---|---|---|---|---|
| APIRoutes-001 | [APIRoutes.swift:425](../Sources/PaperangLab/APIRoutes.swift#L425) | apiPage | Label | 本机HTTP API | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-002 | [APIRoutes.swift:426](../Sources/PaperangLab/APIRoutes.swift#L426) | apiPage | Text | 供本机程序和你允许的网页调用。默认关闭，仅绑定127.0.0.1；不提供shell执行或终端输入接口。 | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-003 | [APIRoutes.swift:427](../Sources/PaperangLab/APIRoutes.swift#L427) | apiPage | integer | 端口 | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-004 | [APIRoutes.swift:430](../Sources/PaperangLab/APIRoutes.swift#L430) | apiPage | SecureField | Bearer访问令牌 | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-005 | [APIRoutes.swift:431](../Sources/PaperangLab/APIRoutes.swift#L431) | apiPage | Button | 复制令牌 | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-006 | [APIRoutes.swift:432](../Sources/PaperangLab/APIRoutes.swift#L432) | apiPage | Button | 重新生成 | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-007 | [APIRoutes.swift:437](../Sources/PaperangLab/APIRoutes.swift#L437) | apiPage | TextField | 允许的网页Origin，逗号分隔 | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-008 | [APIRoutes.swift:439](../Sources/PaperangLab/APIRoutes.swift#L439) | apiPage | Text | 耗纸由左侧全局开关统一控制；API打印请求还需allowPaper:true。 | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-009 | [APIRoutes.swift:444](../Sources/PaperangLab/APIRoutes.swift#L444) | apiPage | Button | 停止API | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-010 | [APIRoutes.swift:447](../Sources/PaperangLab/APIRoutes.swift#L447) | apiPage | Button | 启动本地API | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-011 | [APIRoutes.swift:449](../Sources/PaperangLab/APIRoutes.swift#L449) | apiPage | Text | http://127.0.0.1:\(String(lab.apiPort)) | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-012 | [APIRoutes.swift:451](../Sources/PaperangLab/APIRoutes.swift#L451) | apiPage | GroupBox | v2接口 | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-013 | [APIRoutes.swift:453](../Sources/PaperangLab/APIRoutes.swift#L453) | apiPage | Text | GET /v2/status · /v2/devices · /v2/protocol · /v2/settings\nGET /v2/jobs · /v2/jobs/{id} · /v2/logs · /v2/openapi\nPOST /v2/scan · /v2/connect · /v2/disconnect · /v2/query\nPOST /v2/render · /v2/jobs · /v2/jobs/{id}/cancel · /v2/feed | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-014 | [APIRoutes.swift:459](../Sources/PaperangLab/APIRoutes.swift#L459) | apiPage | Text | 所有实际请求需要Authorization: Bearer <令牌>。跨域网页还需匹配Origin白名单；不支持通配符。打印返回任务ID，不会自动重试。HTTP请求上限2MB、最多8个并发连接；打印、GUI和纸带终端共用同一设备锁。 | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| APIRoutes-015 | [APIRoutes.swift:462](../Sources/PaperangLab/APIRoutes.swift#L462) | apiPage | Text | 完整说明和示例客户端：工程 doc/API.md、doc/openapi.json、Clients/。不要将本机令牌硬编码进公开网页。 | 模块已定位，逐项行为待核对 | LocalApiPage.cs / LoopbackServer.cs / LocalApiChecks.cs |
| App-001 | [App.swift:46](../Sources/PaperangLab/App.swift#L46) | App | Button | About Paperang Studio | 模块已定位，逐项行为待核对 | PreferencesPage.cs / WorkspaceWindow.cs |
| App-002 | [App.swift:47](../Sources/PaperangLab/App.swift#L47) | App | Button | 检查 GitHub 更新… | 存在具体差异 | PreferencesPage.cs：有发布页入口，尚缺 WinClient 发行版本比较 |
| App-003 | [App.swift:52](../Sources/PaperangLab/App.swift#L52) | App | Button | 进入托盘模式 | 模块已定位，逐项行为待核对 | PreferencesPage.cs / WorkspaceWindow.cs |
| App-004 | [App.swift:56](../Sources/PaperangLab/App.swift#L56) | App | Button | 打印工作台 | 模块已定位，逐项行为待核对 | PreferencesPage.cs / WorkspaceWindow.cs |
| App-005 | [App.swift:58](../Sources/PaperangLab/App.swift#L58) | App | Button | 打印机 | 模块已定位，逐项行为待核对 | PreferencesPage.cs / WorkspaceWindow.cs |
| App-006 | [App.swift:60](../Sources/PaperangLab/App.swift#L60) | App | Button | 打印记录 | 模块已定位，逐项行为待核对 | PreferencesPage.cs / WorkspaceWindow.cs |
| App-007 | [App.swift:92](../Sources/PaperangLab/App.swift#L92) | body | Label | Paperang Studio | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-008 | [App.swift:93](../Sources/PaperangLab/App.swift#L93) | body | Text | P2 · 打印与调试 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-009 | [App.swift:94](../Sources/PaperangLab/App.swift#L94) | body | List | List(selection: $lab.page) | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-010 | [App.swift:96](../Sources/PaperangLab/App.swift#L96) | body | Label | 打印工作台 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-011 | [App.swift:97](../Sources/PaperangLab/App.swift#L97) | body | Label | 打印机 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-012 | [App.swift:98](../Sources/PaperangLab/App.swift#L98) | body | Label | 打印记录 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-013 | [App.swift:99](../Sources/PaperangLab/App.swift#L99) | body | Label | 纸带终端 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-014 | [App.swift:100](../Sources/PaperangLab/App.swift#L100) | body | Label | 本地 API | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-015 | [App.swift:101](../Sources/PaperangLab/App.swift#L101) | body | Label | 远程打印 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-016 | [App.swift:104](../Sources/PaperangLab/App.swift#L104) | body | Label | 工程界面 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-017 | [App.swift:107](../Sources/PaperangLab/App.swift#L107) | body | Toggle | 显示工程界面 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-018 | [App.swift:116](../Sources/PaperangLab/App.swift#L116) | body | Toggle | 全局允许耗纸 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-019 | [App.swift:129](../Sources/PaperangLab/App.swift#L129) | body | Text | 工作台 · 终端 · 远程 · API · 送纸已允许 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-020 | [App.swift:132](../Sources/PaperangLab/App.swift#L132) | body | Label | 未连接 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-021 | [App.swift:136](../Sources/PaperangLab/App.swift#L136) | body | Text | Text(lab.state).font(.caption).textSelection(.enabled) | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-022 | [App.swift:137](../Sources/PaperangLab/App.swift#L137) | body | Text | Text(lab.batterySummary).font(.caption) | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-023 | [App.swift:140](../Sources/PaperangLab/App.swift#L140) | body | Button | 刷新状态 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / BuildPrinter + RefreshSession |
| App-024 | [App.swift:141](../Sources/PaperangLab/App.swift#L141) | body | Button | 断开连接 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / BuildPrinter + ClientHost.DisconnectAsync |
| App-025 | [App.swift:144](../Sources/PaperangLab/App.swift#L144) | body | Button | 连接打印机 | 有对应实现，未逐控件验收 | PrinterDiscovery.cs / ConnectPrinterSelection + ClientHost.cs：选择、连接、取消、结果状态 |
| App-026 | [App.swift:150](../Sources/PaperangLab/App.swift#L150) | body | Button | 进入托盘模式 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-027 | [App.swift:151](../Sources/PaperangLab/App.swift#L151) | body | Text | v | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-028 | [App.swift:152](../Sources/PaperangLab/App.swift#L152) | body | Text | Text(AppIdentity.copyright).font(.caption).foregroundStyle(.secondary).textSelection(.enabled) | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-029 | [App.swift:161](../Sources/PaperangLab/App.swift#L161) | body | Label | exclamationmark.triangle.fill | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-030 | [App.swift:168](../Sources/PaperangLab/App.swift#L168) | body | Text | Text(lab.notice).font(.callout).foregroundStyle(.secondary) | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceDesk.cs / PreferencesPage.cs |
| App-031 | [App.swift:198](../Sources/PaperangLab/App.swift#L198) | header | Text | Text(lab.page).font(.largeTitle.bold()).accessibilityAddTraits(.isHeader) | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / ClientHost.cs |
| App-032 | [App.swift:199](../Sources/PaperangLab/App.swift#L199) | header | Text | Text(pageSubtitle).font(.callout).foregroundStyle(.secondary) | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / ClientHost.cs |
| App-033 | [App.swift:204](../Sources/PaperangLab/App.swift#L204) | header | ProgressView | 正在处理打印机操作 | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / ClientHost.cs |
| App-034 | [App.swift:206](../Sources/PaperangLab/App.swift#L206) | header | Text | P2 / 300 DPI | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / ClientHost.cs |
| App-035 | [App.swift:246](../Sources/PaperangLab/App.swift#L246) | workspace | Menu | 快捷预设 | 有对应实现，未逐控件验收 | WorkspaceDesk.cs / quickPreset |
| App-036 | [App.swift:249](../Sources/PaperangLab/App.swift#L249) | workspace | Button | Button(name) | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceEditing.cs |
| App-037 | [App.swift:254](../Sources/PaperangLab/App.swift#L254) | workspace | Button | 导入预设 | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / PresetFile |
| App-038 | [App.swift:255](../Sources/PaperangLab/App.swift#L255) | workspace | Button | 保存预设 | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / PresetFile |
| App-039 | [App.swift:257](../Sources/PaperangLab/App.swift#L257) | workspace | Text | Text(lab.mediaSummary).font(.caption).foregroundStyle(.secondary) | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceEditing.cs |
| App-040 | [App.swift:281](../Sources/PaperangLab/App.swift#L281) | workspace | Label | 最终黑白预览 | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceEditing.cs |
| App-041 | [App.swift:285](../Sources/PaperangLab/App.swift#L285) | workspace | ProgressView | ProgressView().controlSize(.small) | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceEditing.cs |
| App-042 | [App.swift:288](../Sources/PaperangLab/App.swift#L288) | workspace | Text | Text(lab.previewSummary).font(.caption.monospaced()).foregroundStyle(.secondary) | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceEditing.cs |
| App-043 | [App.swift:291](../Sources/PaperangLab/App.swift#L291) | workspace | Text | Text(lab.previewError).foregroundStyle(.orange).textSelection(.enabled) | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceEditing.cs |
| App-044 | [App.swift:303](../Sources/PaperangLab/App.swift#L303) | workspace | Text | 输入文字或导入图片，调整参数后自动更新 | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceEditing.cs |
| App-045 | [App.swift:309](../Sources/PaperangLab/App.swift#L309) | workspace | Button | 导出PNG | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / Export |
| App-046 | [App.swift:313](../Sources/PaperangLab/App.swift#L313) | workspace | Button | 导出位图数据 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / Export |
| App-047 | [App.swift:318](../Sources/PaperangLab/App.swift#L318) | workspace | Text | 1黑0白 · 72字节/行 | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceEditing.cs |
| App-048 | [App.swift:355](../Sources/PaperangLab/App.swift#L355) | input | Picker | 内容 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-049 | [App.swift:357](../Sources/PaperangLab/App.swift#L357) | input | Text | 文字 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-050 | [App.swift:358](../Sources/PaperangLab/App.swift#L358) | input | Text | 图片 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-051 | [App.swift:361](../Sources/PaperangLab/App.swift#L361) | input | Text | 高级排版 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-052 | [App.swift:365](../Sources/PaperangLab/App.swift#L365) | input | Text | 位图 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-053 | [App.swift:370](../Sources/PaperangLab/App.swift#L370) | input | Button | 高级排版… | 有对应实现，未逐控件验收 | WorkspaceDesk.cs / BuildFreeLayout + LayerEditing.cs：可编辑文字、标签、QR、图片；确认/取消草稿 |
| App-054 | [App.swift:374](../Sources/PaperangLab/App.swift#L374) | input | Button | 插入二维码… | 有对应实现，未逐控件验收 | WorkspaceFiles.cs / Qr：内容格式、纠错、模块形态与外框；18 种组合经独立 ZXing 解码 |
| App-055 | [App.swift:379](../Sources/PaperangLab/App.swift#L379) | input | Button | 导入位图… | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / Import |
| App-056 | [App.swift:382](../Sources/PaperangLab/App.swift#L382) | input | Text | Text(lab.importedRasterName).lineLimit(2).textSelection(.enabled) | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-057 | [App.swift:383](../Sources/PaperangLab/App.swift#L383) | input | Text | 直接使用原始黑白点阵。请核对右侧预览后打印。 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-058 | [App.swift:387](../Sources/PaperangLab/App.swift#L387) | input | TextEditor | 支持中文、换行和自动折行；最多10000字符，最终高度不超过1400像素。 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-059 | [App.swift:389](../Sources/PaperangLab/App.swift#L389) | input | Text | 支持中文、换行和自动折行；最多10000字符，最终高度不超过1400像素。 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-060 | [App.swift:392](../Sources/PaperangLab/App.swift#L392) | input | Button | 选择图片… | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / Import |
| App-061 | [App.swift:393](../Sources/PaperangLab/App.swift#L393) | input | Text | Text(lab.imageName).lineLimit(2).textSelection(.enabled) | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-062 | [App.swift:394](../Sources/PaperangLab/App.swift#L394) | input | Text | PNG / JPEG / HEIC / TIFF / BMP / GIF首帧，透明区域先合成白底，自动处理照片方向。 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-063 | [App.swift:400](../Sources/PaperangLab/App.swift#L400) | input | Label | 打印内容 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / WorkspaceFiles.cs / LayerEditing.cs |
| App-064 | [App.swift:411](../Sources/PaperangLab/App.swift#L411) | layout | SystemFontPicker | SystemFontPicker(selection: $lab.settings.fontName) | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / WorkspaceDesk.cs |
| App-065 | [App.swift:412](../Sources/PaperangLab/App.swift#L412) | layout | slider | 字号 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / font |
| App-066 | [App.swift:413](../Sources/PaperangLab/App.swift#L413) | layout | Toggle | 粗体 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / bold |
| App-067 | [App.swift:414](../Sources/PaperangLab/App.swift#L414) | layout | slider | 行间距 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / spacing |
| App-068 | [App.swift:417](../Sources/PaperangLab/App.swift#L417) | layout | Picker | 缩放方式 | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / imageFit |
| App-069 | [App.swift:421](../Sources/PaperangLab/App.swift#L421) | layout | Text | Text($0) | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / WorkspaceDesk.cs |
| App-070 | [App.swift:424](../Sources/PaperangLab/App.swift#L424) | layout | Picker | 旋转 | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / rotation |
| App-071 | [App.swift:428](../Sources/PaperangLab/App.swift#L428) | layout | Text | \($0)° | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / WorkspaceDesk.cs |
| App-072 | [App.swift:433](../Sources/PaperangLab/App.swift#L433) | layout | integer | 裁切高度 px | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / cropHeight |
| App-073 | [App.swift:436](../Sources/PaperangLab/App.swift#L436) | layout | Picker | 对齐 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / alignment |
| App-074 | [App.swift:440](../Sources/PaperangLab/App.swift#L440) | layout | Text | Text($0) | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / WorkspaceDesk.cs |
| App-075 | [App.swift:443](../Sources/PaperangLab/App.swift#L443) | layout | integer | 左右最小边距 px | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / margin |
| App-076 | [App.swift:444](../Sources/PaperangLab/App.swift#L444) | layout | integer | 上下边距 px | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / marginY |
| App-077 | [App.swift:445](../Sources/PaperangLab/App.swift#L445) | layout | integer | 内容宽度 px | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / contentWidth |
| App-078 | [App.swift:446](../Sources/PaperangLab/App.swift#L446) | layout | Text | 左右边距×2 + 内容宽度 ≤ 576。超出时暂停打印并显示原因，不会静默改变宽度。 | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / WorkspaceDesk.cs |
| App-079 | [App.swift:450](../Sources/PaperangLab/App.swift#L450) | layout | Label | 排版与尺寸 | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / WorkspaceDesk.cs |
| App-080 | [App.swift:459](../Sources/PaperangLab/App.swift#L459) | imageProcessing | Picker | 黑白方式 | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / conversion |
| App-081 | [App.swift:463](../Sources/PaperangLab/App.swift#L463) | imageProcessing | Text | Text($0) | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / WorkspaceImage.cs |
| App-082 | [App.swift:466](../Sources/PaperangLab/App.swift#L466) | imageProcessing | slider | 黑白阈值 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / threshold |
| App-083 | [App.swift:467](../Sources/PaperangLab/App.swift#L467) | imageProcessing | slider | 亮度 | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / brightness |
| App-084 | [App.swift:468](../Sources/PaperangLab/App.swift#L468) | imageProcessing | slider | 对比度 | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / contrast |
| App-085 | [App.swift:469](../Sources/PaperangLab/App.swift#L469) | imageProcessing | slider | Gamma | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / gamma |
| App-086 | [App.swift:470](../Sources/PaperangLab/App.swift#L470) | imageProcessing | Toggle | 黑白反相 | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / invert |
| App-087 | [App.swift:471](../Sources/PaperangLab/App.swift#L471) | imageProcessing | Text | 文字、线稿适合阈值；照片可尝试抖动。预览和实际发送共用同一份最终位图。 | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / WorkspaceImage.cs |
| App-088 | [App.swift:475](../Sources/PaperangLab/App.swift#L475) | imageProcessing | Label | 黑白处理 | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / WorkspaceImage.cs |
| App-089 | [App.swift:484](../Sources/PaperangLab/App.swift#L484) | output | integer | 打印浓度 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / density |
| App-090 | [App.swift:485](../Sources/PaperangLab/App.swift#L485) | output | integer | 份数 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / copies |
| App-091 | [App.swift:486](../Sources/PaperangLab/App.swift#L486) | output | integer | 打印前送纸 行 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / before |
| App-092 | [App.swift:487](../Sources/PaperangLab/App.swift#L487) | output | integer | 打印后送纸 行 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / feed |
| App-093 | [App.swift:488](../Sources/PaperangLab/App.swift#L488) | output | Toggle | 每份后都送纸 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / between |
| App-094 | [App.swift:489](../Sources/PaperangLab/App.swift#L489) | output | Text | 关闭时仅在整批末尾送纸。推荐浓度100、批后送纸160行。 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / LocalPrintOptions.cs |
| App-095 | [App.swift:490](../Sources/PaperangLab/App.swift#L490) | output | Button | 仅应用浓度并回读 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / LocalPrintOptions.cs |
| App-096 | [App.swift:494](../Sources/PaperangLab/App.swift#L494) | output | Label | 打印与送纸 | 模块已定位，逐项行为待核对 | WorkspaceWindow.cs / LocalPrintOptions.cs |
| App-097 | [App.swift:503](../Sources/PaperangLab/App.swift#L503) | footer | ProgressView | ProgressView(value: lab.progress) | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceWindow.cs |
| App-098 | [App.swift:504](../Sources/PaperangLab/App.swift#L504) | footer | Text | Text(lab.jobStatus).font(.caption) | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceWindow.cs |
| App-099 | [App.swift:507](../Sources/PaperangLab/App.swift#L507) | footer | Label | checkmark.circle | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceWindow.cs |
| App-100 | [App.swift:512](../Sources/PaperangLab/App.swift#L512) | footer | Button | 仅送纸 \(lab.settings.feedAfter)行 | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceWindow.cs |
| App-101 | [App.swift:516](../Sources/PaperangLab/App.swift#L516) | footer | Button | 停止并断开 | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceWindow.cs |
| App-102 | [App.swift:518](../Sources/PaperangLab/App.swift#L518) | footer | Button | 打印 \(lab.settings.copies)份 | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceWindow.cs |
| App-103 | [App.swift:521](../Sources/PaperangLab/App.swift#L521) | footer | Text | 停止会阻止后续发送，无法撤回已进入打印机的内容。 | 模块已定位，逐项行为待核对 | WorkspaceDesk.cs / WorkspaceWindow.cs |
| App-104 | [App.swift:531](../Sources/PaperangLab/App.swift#L531) | devices | Button | 扫描中… | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-105 | [App.swift:532](../Sources/PaperangLab/App.swift#L532) | devices | Button | 重新初始化扫描 | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-106 | [App.swift:533](../Sources/PaperangLab/App.swift#L533) | devices | Button | 刷新电量与纸仓 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / BuildPrinter：独立刷新与实时状态 |
| App-107 | [App.swift:534](../Sources/PaperangLab/App.swift#L534) | devices | Toggle | 展示全部蓝牙设备 | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-108 | [App.swift:536](../Sources/PaperangLab/App.swift#L536) | devices | Text | Text(lab.scanSummary).font(.caption).foregroundStyle(.secondary).textSelection(.enabled) | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-109 | [App.swift:537](../Sources/PaperangLab/App.swift#L537) | devices | List | List(lab.visibleDevices) | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-110 | [App.swift:545](../Sources/PaperangLab/App.swift#L545) | devices | Text | Text(d.name).font(.headline) | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-111 | [App.swift:546](../Sources/PaperangLab/App.swift#L546) | devices | Text |  · 信号 \(d.rssi) dBm | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-112 | [App.swift:548](../Sources/PaperangLab/App.swift#L548) | devices | Text | Text(d.id.uuidString).font(.caption.monospaced()).textSelection(.enabled) | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-113 | [App.swift:551](../Sources/PaperangLab/App.swift#L551) | devices | Button | 连接 | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-114 | [App.swift:557](../Sources/PaperangLab/App.swift#L557) | devices | GroupBox | 当前设备 | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-115 | [App.swift:561](../Sources/PaperangLab/App.swift#L561) | devices | Text | Text(lab.connectedName).font(.headline) | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-116 | [App.swift:562](../Sources/PaperangLab/App.swift#L562) | devices | Text | Text(lab.mediaSummary) | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-117 | [App.swift:563](../Sources/PaperangLab/App.swift#L563) | devices | Text | Text(lab.batterySummary) | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-118 | [App.swift:564](../Sources/PaperangLab/App.swift#L564) | devices | Text | 充电：暂无独立实时字段 · LED控制：当前协议未确认 | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-119 | [App.swift:565](../Sources/PaperangLab/App.swift#L565) | devices | Text | 更多原始字段与GATT信息可在左侧开启工程界面查看。 | 模块已定位，逐项行为待核对 | PrinterDiscovery.cs / ClientHost.cs / BlePrinter.cs / WorkspaceFlowChecks.cs（实时发现与扫描中选择） |
| App-120 | [App.swift:574](../Sources/PaperangLab/App.swift#L574) | history | Text | 仅保存当前会话最近30项，不会自动重打失败任务。 | 模块已定位，逐项行为待核对 | WorkspaceLibrary.cs / WorkspaceWindow.cs |
| App-121 | [App.swift:577](../Sources/PaperangLab/App.swift#L577) | history | Text | 在工作台完成预览后发送一批打印 | 模块已定位，逐项行为待核对 | WorkspaceLibrary.cs / WorkspaceWindow.cs |
| App-122 | [App.swift:580](../Sources/PaperangLab/App.swift#L580) | history | List | List(lab.jobs.reversed()) | 模块已定位，逐项行为待核对 | WorkspaceLibrary.cs / WorkspaceWindow.cs |
| App-123 | [App.swift:587](../Sources/PaperangLab/App.swift#L587) | history | Text | Text(j.title).bold() | 模块已定位，逐项行为待核对 | WorkspaceLibrary.cs / WorkspaceWindow.cs |
| App-124 | [App.swift:589](../Sources/PaperangLab/App.swift#L589) | history | Text | Text(j.date, style: .time) | 模块已定位，逐项行为待核对 | WorkspaceLibrary.cs / WorkspaceWindow.cs |
| App-125 | [App.swift:591](../Sources/PaperangLab/App.swift#L591) | history | Text | Text(j.detail) | 模块已定位，逐项行为待核对 | WorkspaceLibrary.cs / WorkspaceWindow.cs |
| App-126 | [App.swift:592](../Sources/PaperangLab/App.swift#L592) | history | Text | Text(j.status).foregroundStyle(.secondary) | 模块已定位，逐项行为待核对 | WorkspaceLibrary.cs / WorkspaceWindow.cs |
| App-127 | [App.swift:602](../Sources/PaperangLab/App.swift#L602) | engineering | Picker | 工程页面 | 模块已定位，逐项行为待核对 | EngineeringPage.cs |
| App-128 | [App.swift:606](../Sources/PaperangLab/App.swift#L606) | engineering | Text | Text($0) | 模块已定位，逐项行为待核对 | EngineeringPage.cs |
| App-129 | [App.swift:625](../Sources/PaperangLab/App.swift#L625) | fields | Button | 读取全部已知GET | 有对应实现，未逐控件验收 | EngineeringPage.cs / BuildEngineering |
| App-130 | [App.swift:626](../Sources/PaperangLab/App.swift#L626) | fields | Button | 导出会话JSON | 有对应实现，未逐控件验收 | EngineeringPage.cs：字段、任务、传输参数与收发帧记录 |
| App-131 | [App.swift:631](../Sources/PaperangLab/App.swift#L631) | fields | TableColumn | 字段 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Queries.cs |
| App-132 | [App.swift:633](../Sources/PaperangLab/App.swift#L633) | fields | Text | Text($0.title) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Queries.cs |
| App-133 | [App.swift:635](../Sources/PaperangLab/App.swift#L635) | fields | TableColumn | 值 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Queries.cs |
| App-134 | [App.swift:637](../Sources/PaperangLab/App.swift#L637) | fields | Text | Text($0.value).textSelection(.enabled) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Queries.cs |
| App-135 | [App.swift:639](../Sources/PaperangLab/App.swift#L639) | fields | TableColumn | 原始载荷 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Queries.cs |
| App-136 | [App.swift:641](../Sources/PaperangLab/App.swift#L641) | fields | Text | Text($0.raw).font(.caption.monospaced()).textSelection(.enabled) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Queries.cs |
| App-137 | [App.swift:643](../Sources/PaperangLab/App.swift#L643) | fields | TableColumn | 采样时间 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Queries.cs |
| App-138 | [App.swift:645](../Sources/PaperangLab/App.swift#L645) | fields | Text | Text($0.time, style: .time) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Queries.cs |
| App-139 | [App.swift:652](../Sources/PaperangLab/App.swift#L652) | fields | Text | \n | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Queries.cs |
| App-140 | [App.swift:656](../Sources/PaperangLab/App.swift#L656) | fields | Text | ACK不是测量值。开盖位0010、合盖时缺纸位0001已在本机对照确认；其他位不猜测。ISSC/BT5050为蓝牙模块信息。 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Queries.cs |
| App-141 | [App.swift:664](../Sources/PaperangLab/App.swift#L664) | catalog | TextField | 搜索命令、说明、验证等级 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Command.cs |
| App-142 | [App.swift:665](../Sources/PaperangLab/App.swift#L665) | catalog | List | List( Command.all.filter { lab.search.isEmpty | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Command.cs |
| App-143 | [App.swift:678](../Sources/PaperangLab/App.swift#L678) | catalog | Text | Text(c.label).font(.headline.monospaced()) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Command.cs |
| App-144 | [App.swift:680](../Sources/PaperangLab/App.swift#L680) | catalog | Text | Text(c.kind) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Command.cs |
| App-145 | [App.swift:681](../Sources/PaperangLab/App.swift#L681) | catalog | Text | Text(c.evidence).foregroundStyle(.secondary) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Command.cs |
| App-146 | [App.swift:683](../Sources/PaperangLab/App.swift#L683) | catalog | Text | Text(c.detail).textSelection(.enabled) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Command.cs |
| App-147 | [App.swift:686](../Sources/PaperangLab/App.swift#L686) | catalog | Button | 查询 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Command.cs |
| App-148 | [App.swift:694](../Sources/PaperangLab/App.swift#L694) | catalog | Text | 02 cmd index lengthLE payload CRC32LE 03 · CRC会话seed=0 · FF03是额度流控 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Command.cs |
| App-149 | [App.swift:701](../Sources/PaperangLab/App.swift#L701) | raw | Text | 协议指令工作台 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Engineering.cs |
| App-150 | [App.swift:702](../Sources/PaperangLab/App.swift#L702) | raw | Picker | 只读目录 | 有对应实现，未逐控件验收 | EngineeringPage.cs：37 条命令与证据说明，GET 白名单独立限制 |
| App-151 | [App.swift:709](../Sources/PaperangLab/App.swift#L709) | raw | Text | Text($0.label).tag($0.code) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Engineering.cs |
| App-152 | [App.swift:712](../Sources/PaperangLab/App.swift#L712) | raw | Button | 发送GET查询 | 有对应实现，未逐控件验收 | EngineeringPage.cs / ReadDevice |
| App-153 | [App.swift:715](../Sources/PaperangLab/App.swift#L715) | raw | TextField | 命令，例如04 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Engineering.cs |
| App-154 | [App.swift:716](../Sources/PaperangLab/App.swift#L716) | raw | TextField | 载荷，例如01 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Engineering.cs |
| App-155 | [App.swift:718](../Sources/PaperangLab/App.swift#L718) | raw | Toggle | 解锁工程参数写入（每次发送前仍确认） | 有对应实现，未逐控件验收 | EngineeringPage.cs + P2Engineering.cs：相同 8 类写命令及载荷限制 |
| App-156 | [App.swift:719](../Sources/PaperangLab/App.swift#L719) | raw | Text | GET固定载荷01。已开放19浓度、1A送纸、1E关机时间、27间隙、2C纸张类型、14工厂模式、1B/22测试页的格式校验。工厂模式/间隙语义未验证，可能改变持久参数。固件更新、压缩位图及未知指令不开放。 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Engineering.cs |
| App-157 | [App.swift:722](../Sources/PaperangLab/App.swift#L722) | raw | Button | 构造帧并发送一次 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / P2Engineering.cs |
| App-158 | [App.swift:732](../Sources/PaperangLab/App.swift#L732) | transport | Text | 这些参数影响实际分包与等待，不改变576点打印头宽度。默认值来自本机已成功路径。 | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / EngineeringPage.cs / P2Session.cs / UsbP2Session.cs |
| App-159 | [App.swift:733](../Sources/PaperangLab/App.swift#L733) | transport | integer | 图像载荷 bytes（72的倍数） | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / transferFrame + P2Session/UsbP2Session |
| App-160 | [App.swift:734](../Sources/PaperangLab/App.swift#L734) | transport | integer | BLE分段上限 bytes | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / transferChunk + P2Session |
| App-161 | [App.swift:735](../Sources/PaperangLab/App.swift#L735) | transport | integer | BLE写入间隔 ms | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / transferWrite + P2Session |
| App-162 | [App.swift:736](../Sources/PaperangLab/App.swift#L736) | transport | integer | 图像帧间隔 ms | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / transferPause |
| App-163 | [App.swift:737](../Sources/PaperangLab/App.swift#L737) | transport | integer | 单命令超时 秒 | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / transferTimeout |
| App-164 | [App.swift:738](../Sources/PaperangLab/App.swift#L738) | transport | integer | 每份数据后等待 秒 | 有对应实现，未逐控件验收 | WorkspaceEditing.cs / transferFinish |
| App-165 | [App.swift:739](../Sources/PaperangLab/App.swift#L739) | transport | Text | 设备报告最大写入 \(lab.packetLimit) bytes；实际取设备与用户上限中的较小值。协议握手和CRC固定使用已验证参数，不接受无依据的宽度/固件修改。 | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / EngineeringPage.cs / P2Session.cs / UsbP2Session.cs |
| App-166 | [App.swift:740](../Sources/PaperangLab/App.swift#L740) | transport | Button | 导出完整参数预设 | 模块已定位，逐项行为待核对 | WorkspaceEditing.cs / EngineeringPage.cs / P2Session.cs / UsbP2Session.cs |
| App-167 | [App.swift:750](../Sources/PaperangLab/App.swift#L750) | logView | TextField | 过滤日志 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-168 | [App.swift:751](../Sources/PaperangLab/App.swift#L751) | logView | Button | 导出JSON | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-169 | [App.swift:752](../Sources/PaperangLab/App.swift#L752) | logView | Button | 清空显示 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-170 | [App.swift:771](../Sources/PaperangLab/App.swift#L771) | logView | Text | Text(r.time, style: .time).frame(width: 75, alignment: .leading) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-171 | [App.swift:772](../Sources/PaperangLab/App.swift#L772) | logView | Text | ERROR | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-172 | [App.swift:774](../Sources/PaperangLab/App.swift#L774) | logView | Text | Text(r.text).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-173 | [App.swift:779](../Sources/PaperangLab/App.swift#L779) | logView | Text | 最新日志在上 · 最近4000条 · 完整帧、BLE分段、原始通知、CRC与错误 | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-174 | [App.swift:782](../Sources/PaperangLab/App.swift#L782) | logView | slider | slider(_ title: String, value: Binding<Double>, range: ClosedRange<Double>, step: Double) -> some View | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-175 | [App.swift:788](../Sources/PaperangLab/App.swift#L788) | logView | Text | Text(title) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-176 | [App.swift:790](../Sources/PaperangLab/App.swift#L790) | logView | Text | Text(value.wrappedValue.formatted(.number.precision(.fractionLength(step < 1 ? 1 : 0)))) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-177 | [App.swift:793](../Sources/PaperangLab/App.swift#L793) | logView | Slider | Slider(value: value, in: range, step: step) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-178 | [App.swift:796](../Sources/PaperangLab/App.swift#L796) | logView | integer | integer(_ title: String, value: Binding<Int>, range: ClosedRange<Int>, step: Int = 1) -> some View | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-179 | [App.swift:800](../Sources/PaperangLab/App.swift#L800) | logView | Text | Text(title) | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-180 | [App.swift:802](../Sources/PaperangLab/App.swift#L802) | logView | TextField |  | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| App-181 | [App.swift:804](../Sources/PaperangLab/App.swift#L804) | logView | Stepper |  | 模块已定位，逐项行为待核对 | EngineeringPage.cs / ProtocolTrace.cs |
| PaperEditor-001 | [PaperEditor.swift:30](../Sources/PaperangLab/PaperEditor.swift#L30) | body | Label | chevron.up.chevron.down | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-002 | [PaperEditor.swift:37](../Sources/PaperangLab/PaperEditor.swift#L37) | body | TextField | 搜索系统字体 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-003 | [PaperEditor.swift:43](../Sources/PaperangLab/PaperEditor.swift#L43) | body | Text | 常用字体 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-004 | [PaperEditor.swift:46](../Sources/PaperangLab/PaperEditor.swift#L46) | body | Text | 全部系统字体 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-005 | [PaperEditor.swift:73](../Sources/PaperangLab/PaperEditor.swift#L73) | body | Text | Text(font).font(.custom(font, size: 13)) | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-006 | [PaperEditor.swift:118](../Sources/PaperangLab/PaperEditor.swift#L118) | body | Text | 高级排版 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-007 | [PaperEditor.swift:120](../Sources/PaperangLab/PaperEditor.swift#L120) | body | Stepper | 纸长 \(session.draft.height) 点 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-008 | [PaperEditor.swift:128](../Sources/PaperangLab/PaperEditor.swift#L128) | body | Button | Button(kind.rawValue) | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-009 | [PaperEditor.swift:138](../Sources/PaperangLab/PaperEditor.swift#L138) | body | Text | 拖入对象或 Finder 图片，拖动纸面对象调整位置 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-010 | [PaperEditor.swift:157](../Sources/PaperangLab/PaperEditor.swift#L157) | body | Text | 选择纸上的对象以调整格式、位置和尺寸。 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-011 | [PaperEditor.swift:164](../Sources/PaperangLab/PaperEditor.swift#L164) | body | Text | Text(session.error).foregroundStyle(.red).textSelection(.enabled) | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-012 | [PaperEditor.swift:168](../Sources/PaperangLab/PaperEditor.swift#L168) | body | Text | 576 点宽 · 对象按纸面所见顺序叠放 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-013 | [PaperEditor.swift:170](../Sources/PaperangLab/PaperEditor.swift#L170) | body | Button | 取消 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-014 | [PaperEditor.swift:174](../Sources/PaperangLab/PaperEditor.swift#L174) | body | Button | 确认并渲染 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-015 | [PaperEditor.swift:365](../Sources/PaperangLab/PaperEditor.swift#L365) | paper | Stepper | \(title) \(Int(frame.wrappedValue[keyPath: key])) | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-016 | [PaperEditor.swift:400](../Sources/PaperangLab/PaperEditor.swift#L400) | paper | Text | Text(layer.wrappedValue.kind.rawValue).font(.headline) | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-017 | [PaperEditor.swift:409](../Sources/PaperangLab/PaperEditor.swift#L409) | paper | Picker | 内容格式 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-018 | [PaperEditor.swift:413](../Sources/PaperangLab/PaperEditor.swift#L413) | paper | Text | Text($0.rawValue).tag($0) | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-019 | [PaperEditor.swift:416](../Sources/PaperangLab/PaperEditor.swift#L416) | paper | TextEditor | TextEditor(text: layer.qr.content).frame(height: 65) | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-020 | [PaperEditor.swift:419](../Sources/PaperangLab/PaperEditor.swift#L419) | paper | Picker | 模块形态 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-021 | [PaperEditor.swift:423](../Sources/PaperangLab/PaperEditor.swift#L423) | paper | Text | Text($0.rawValue).tag($0) | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-022 | [PaperEditor.swift:426](../Sources/PaperangLab/PaperEditor.swift#L426) | paper | Picker | 纠错级别 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-023 | [PaperEditor.swift:430](../Sources/PaperangLab/PaperEditor.swift#L430) | paper | Text | Text($0).tag($0) | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-024 | [PaperEditor.swift:433](../Sources/PaperangLab/PaperEditor.swift#L433) | paper | Toggle | 加外框 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-025 | [PaperEditor.swift:437](../Sources/PaperangLab/PaperEditor.swift#L437) | paper | Button | 字体与样式… | 有对应实现，未逐控件验收 | LayerEditing.cs：原生 RichEditBox 选区字体、字号、粗斜体与下划线 |
| PaperEditor-026 | [PaperEditor.swift:443](../Sources/PaperangLab/PaperEditor.swift#L443) | paper | Button | 左对齐 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-027 | [PaperEditor.swift:447](../Sources/PaperangLab/PaperEditor.swift#L447) | paper | Button | 居中 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-028 | [PaperEditor.swift:451](../Sources/PaperangLab/PaperEditor.swift#L451) | paper | Button | 右对齐 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-029 | [PaperEditor.swift:456](../Sources/PaperangLab/PaperEditor.swift#L456) | paper | Text | 在编辑框中选中单字或一段文字，再打开字体与样式。对齐作用于整个文字框。 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-030 | [PaperEditor.swift:459](../Sources/PaperangLab/PaperEditor.swift#L459) | paper | Text | 图片按原比例适配对象边框 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperEditor-031 | [PaperEditor.swift:464](../Sources/PaperangLab/PaperEditor.swift#L464) | paper | Button | 移到最上层 | 有对应实现，未逐控件验收 | WorkspaceDesk.cs / BuildFreeLayout |
| PaperEditor-032 | [PaperEditor.swift:475](../Sources/PaperangLab/PaperEditor.swift#L475) | paper | Button | 删除 | 模块已定位，逐项行为待核对 | LayerEditing.cs / WorkspaceDesk.cs / LayerChecks.cs |
| PaperTTY-001 | [PaperTTY.swift:239](../Sources/PaperangLab/PaperTTY.swift#L239) | terminalPage | Label | terminal | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-002 | [PaperTTY.swift:241](../Sources/PaperangLab/PaperTTY.swift#L241) | terminalPage | Text | zsh · PID \(lab.terminal?.pid ?? 0) · TERM=dumb | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-003 | [PaperTTY.swift:243](../Sources/PaperangLab/PaperTTY.swift#L243) | terminalPage | Button | 导出文本 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-004 | [PaperTTY.swift:247](../Sources/PaperangLab/PaperTTY.swift#L247) | terminalPage | GroupBox | 启动真实本地PTY | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-005 | [PaperTTY.swift:253](../Sources/PaperangLab/PaperTTY.swift#L253) | terminalPage | TextField | 工作目录 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-006 | [PaperTTY.swift:254](../Sources/PaperangLab/PaperTTY.swift#L254) | terminalPage | Button | 选择目录 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-007 | [PaperTTY.swift:258](../Sources/PaperangLab/PaperTTY.swift#L258) | terminalPage | integer | 列 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-008 | [PaperTTY.swift:259](../Sources/PaperangLab/PaperTTY.swift#L259) | terminalPage | integer | 行 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-009 | [PaperTTY.swift:260](../Sources/PaperangLab/PaperTTY.swift#L260) | terminalPage | Toggle | 加载用户.zshrc | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-010 | [PaperTTY.swift:261](../Sources/PaperangLab/PaperTTY.swift#L261) | terminalPage | Button | 启动终端 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-011 | [PaperTTY.swift:273](../Sources/PaperangLab/PaperTTY.swift#L273) | terminalPage | Text | 等待终端输出… | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-012 | [PaperTTY.swift:288](../Sources/PaperangLab/PaperTTY.swift#L288) | terminalPage | SecureField | 隐私输入 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-013 | [PaperTTY.swift:291](../Sources/PaperangLab/PaperTTY.swift#L291) | terminalPage | TextField | 输入命令或交互式回答，按回车发送 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-014 | [PaperTTY.swift:293](../Sources/PaperangLab/PaperTTY.swift#L293) | terminalPage | Button | 发送回车 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-015 | [PaperTTY.swift:297](../Sources/PaperangLab/PaperTTY.swift#L297) | terminalPage | Button | Ctrl-C | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-016 | [PaperTTY.swift:301](../Sources/PaperangLab/PaperTTY.swift#L301) | terminalPage | Button | Ctrl-D | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-017 | [PaperTTY.swift:305](../Sources/PaperangLab/PaperTTY.swift#L305) | terminalPage | Button | Ctrl-Z | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-018 | [PaperTTY.swift:309](../Sources/PaperangLab/PaperTTY.swift#L309) | terminalPage | Button | Tab | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-019 | [PaperTTY.swift:313](../Sources/PaperangLab/PaperTTY.swift#L313) | terminalPage | Button | ↑ | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-020 | [PaperTTY.swift:317](../Sources/PaperangLab/PaperTTY.swift#L317) | terminalPage | Button | ↓ | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-021 | [PaperTTY.swift:321](../Sources/PaperangLab/PaperTTY.swift#L321) | terminalPage | Button | 停止shell | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-022 | [PaperTTY.swift:322](../Sources/PaperangLab/PaperTTY.swift#L322) | terminalPage | Toggle | 隐私输入：暂停记录 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-023 | [PaperTTY.swift:332](../Sources/PaperangLab/PaperTTY.swift#L332) | terminalPage | GroupBox | 实体纸带 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-024 | [PaperTTY.swift:338](../Sources/PaperangLab/PaperTTY.swift#L338) | terminalPage | Toggle | 自动打印终端输出 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-025 | [PaperTTY.swift:340](../Sources/PaperangLab/PaperTTY.swift#L340) | terminalPage | Text | 待提交 \(lab.ttyPendingCount) 字符 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-026 | [PaperTTY.swift:344](../Sources/PaperangLab/PaperTTY.swift#L344) | terminalPage | integer | 空闲批处理 秒 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-027 | [PaperTTY.swift:345](../Sources/PaperangLab/PaperTTY.swift#L345) | terminalPage | integer | 每批送纸 行 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-028 | [PaperTTY.swift:346](../Sources/PaperangLab/PaperTTY.swift#L346) | terminalPage | slider | 终端字号 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-029 | [PaperTTY.swift:350](../Sources/PaperangLab/PaperTTY.swift#L350) | terminalPage | Button | 立即打印缓冲 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-030 | [PaperTTY.swift:354](../Sources/PaperangLab/PaperTTY.swift#L354) | terminalPage | Button | 丢弃待打印缓冲 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-031 | [PaperTTY.swift:357](../Sources/PaperangLab/PaperTTY.swift#L357) | terminalPage | Button | 停止打印并断开 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-032 | [PaperTTY.swift:360](../Sources/PaperangLab/PaperTTY.swift#L360) | terminalPage | Text | Text(lab.ttyMessage).font(.caption).textSelection(.enabled) | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| PaperTTY-033 | [PaperTTY.swift:363](../Sources/PaperangLab/PaperTTY.swift#L363) | terminalPage | Text | 这里运行真实shell，命令按当前用户权限执行。纸带模式不模拟可擦除的全屏终端；ANSI颜色会清除。默认不加载用户配置、不自动打印。每批浓度固定100；连续纸带可将额外送纸设为0，末尾在工作台手动送150行撕取。隐私模式开启期间丢弃输出，不会补打印。 | 模块已定位，逐项行为待核对 | TerminalPage.cs / TerminalSession.cs / TerminalChecks.cs（Shell 改为 Windows ConPTY） |
| RelayLayout-001 | [RelayLayout.swift:342](../Sources/PaperangLab/RelayLayout.swift#L342) | body | Label | 打印样式编辑器 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-002 | [RelayLayout.swift:344](../Sources/PaperangLab/RelayLayout.swift#L344) | body | Button | 取消 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-003 | [RelayLayout.swift:348](../Sources/PaperangLab/RelayLayout.swift#L348) | body | Button | 保存并应用 | 有对应实现，未逐控件验收 | TemplatePage.cs：验证草稿后写入加密配置 |
| RelayLayout-004 | [RelayLayout.swift:352](../Sources/PaperangLab/RelayLayout.swift#L352) | body | Picker | 模板 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-005 | [RelayLayout.swift:356](../Sources/PaperangLab/RelayLayout.swift#L356) | body | Text | Text($0.name).tag($0.id) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-006 | [RelayLayout.swift:358](../Sources/PaperangLab/RelayLayout.swift#L358) | body | TextField | 模板名称 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-007 | [RelayLayout.swift:359](../Sources/PaperangLab/RelayLayout.swift#L359) | body | Button | 复制模板 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-008 | [RelayLayout.swift:371](../Sources/PaperangLab/RelayLayout.swift#L371) | body | Menu | 载入预设 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-009 | [RelayLayout.swift:376](../Sources/PaperangLab/RelayLayout.swift#L376) | body | Button | Button(name) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-010 | [RelayLayout.swift:388](../Sources/PaperangLab/RelayLayout.swift#L388) | body | Button | 导入JSON | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-011 | [RelayLayout.swift:389](../Sources/PaperangLab/RelayLayout.swift#L389) | body | Button | 导出JSON | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-012 | [RelayLayout.swift:390](../Sources/PaperangLab/RelayLayout.swift#L390) | body | Button | 删除模板 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-013 | [RelayLayout.swift:404](../Sources/PaperangLab/RelayLayout.swift#L404) | body | Text | 区块（拖动排序） | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-014 | [RelayLayout.swift:405](../Sources/PaperangLab/RelayLayout.swift#L405) | body | List | List(selection: $draft.blockID) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-015 | [RelayLayout.swift:413](../Sources/PaperangLab/RelayLayout.swift#L413) | body | Text | Text(b.kind) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-016 | [RelayLayout.swift:415](../Sources/PaperangLab/RelayLayout.swift#L415) | body | Text | Text(Int(b.size).description).foregroundStyle(.secondary) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-017 | [RelayLayout.swift:425](../Sources/PaperangLab/RelayLayout.swift#L425) | body | Menu | 添加区块 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-018 | [RelayLayout.swift:430](../Sources/PaperangLab/RelayLayout.swift#L430) | body | Button | Button(kind) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-019 | [RelayLayout.swift:442](../Sources/PaperangLab/RelayLayout.swift#L442) | body | Button | 删除区块 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-020 | [RelayLayout.swift:459](../Sources/PaperangLab/RelayLayout.swift#L459) | body | Text | 区块属性 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-021 | [RelayLayout.swift:465](../Sources/PaperangLab/RelayLayout.swift#L465) | body | Toggle | 启用 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-022 | [RelayLayout.swift:466](../Sources/PaperangLab/RelayLayout.swift#L466) | body | Picker | 内容 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-023 | [RelayLayout.swift:470](../Sources/PaperangLab/RelayLayout.swift#L470) | body | Text | Text($0) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-024 | [RelayLayout.swift:475](../Sources/PaperangLab/RelayLayout.swift#L475) | body | TextEditor | 变量：{{text}} {{sender}} {{id}} {{time}} {{ip}} {{ua}} {{device}}；id为可打印的短编号。 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-025 | [RelayLayout.swift:477](../Sources/PaperangLab/RelayLayout.swift#L477) | body | Text | 变量：{{text}} {{sender}} {{id}} {{time}} {{ip}} {{ua}} {{device}}；id为可打印的短编号。 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-026 | [RelayLayout.swift:482](../Sources/PaperangLab/RelayLayout.swift#L482) | body | Text | 正文区块同时接收文字和图片。图片按本区块边距等比适配，说明文字紧随图片；字体与对齐用于文字。关闭正文会同时隐藏图片和说明。 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-027 | [RelayLayout.swift:485](../Sources/PaperangLab/RelayLayout.swift#L485) | body | Picker | 字体 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-028 | [RelayLayout.swift:489](../Sources/PaperangLab/RelayLayout.swift#L489) | body | Text | Text($0) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-029 | [RelayLayout.swift:492](../Sources/PaperangLab/RelayLayout.swift#L492) | body | Text | 字号 \(Int(draft.templates[ti].blocks[bi].size)) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-030 | [RelayLayout.swift:493](../Sources/PaperangLab/RelayLayout.swift#L493) | body | Slider | Slider(value: $draft.templates[ti].blocks[bi].size, in: 8...96, step: 1) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-031 | [RelayLayout.swift:494](../Sources/PaperangLab/RelayLayout.swift#L494) | body | Toggle | 粗体 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / bold |
| RelayLayout-032 | [RelayLayout.swift:495](../Sources/PaperangLab/RelayLayout.swift#L495) | body | Picker | 对齐 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / alignment |
| RelayLayout-033 | [RelayLayout.swift:499](../Sources/PaperangLab/RelayLayout.swift#L499) | body | Text | Text($0) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-034 | [RelayLayout.swift:502](../Sources/PaperangLab/RelayLayout.swift#L502) | body | Stepper | 左右边距：\(draft.templates[ti].blocks[bi].margin)点 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-035 | [RelayLayout.swift:505](../Sources/PaperangLab/RelayLayout.swift#L505) | body | Stepper | 上方留白：\(draft.templates[ti].blocks[bi].gap)行 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-036 | [RelayLayout.swift:510](../Sources/PaperangLab/RelayLayout.swift#L510) | body | Button | 上移 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-037 | [RelayLayout.swift:514](../Sources/PaperangLab/RelayLayout.swift#L514) | body | Button | 下移 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-038 | [RelayLayout.swift:521](../Sources/PaperangLab/RelayLayout.swift#L521) | body | Text | 在左侧或纸面选择区块 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-039 | [RelayLayout.swift:524](../Sources/PaperangLab/RelayLayout.swift#L524) | body | Text | Text(draft.message).font(.caption).foregroundStyle(.secondary).textSelection(.enabled) | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelayLayout-040 | [RelayLayout.swift:525](../Sources/PaperangLab/RelayLayout.swift#L525) | body | Text | 编辑的是草稿，保存后才用于真实任务。纸面点击选择区块；拖动可调边距和上方留白。长正文会推动后续区块，不会重叠。 | 模块已定位，逐项行为待核对 | TemplatePage.cs / Templates.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-001 | [RelaySecurityManager.swift:737](../Sources/PaperangLab/RelaySecurityManager.swift#L737) | body | SecureField | C 密钥或完整口令 | 有对应实现，未逐控件验收 | AdministrationPage.cs：独立短期 C 管理会话，不替换接收凭证 |
| RelaySecurityManager-002 | [RelaySecurityManager.swift:738](../Sources/PaperangLab/RelaySecurityManager.swift#L738) | body | Button | 登录 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-003 | [RelaySecurityManager.swift:744](../Sources/PaperangLab/RelaySecurityManager.swift#L744) | body | Button | 所有者私钥登录 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-004 | [RelaySecurityManager.swift:750](../Sources/PaperangLab/RelaySecurityManager.swift#L750) | body | Button | 锁定 | 有对应实现，未逐控件验收 | AdministrationPage.cs / LockAdministration：清除凭证、详情、预览及会话 |
| RelaySecurityManager-005 | [RelaySecurityManager.swift:756](../Sources/PaperangLab/RelaySecurityManager.swift#L756) | body | Picker | 管理类别 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-006 | [RelaySecurityManager.swift:758](../Sources/PaperangLab/RelaySecurityManager.swift#L758) | body | Text | 凭证 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-007 | [RelaySecurityManager.swift:759](../Sources/PaperangLab/RelaySecurityManager.swift#L759) | body | Text | 身份 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-008 | [RelaySecurityManager.swift:760](../Sources/PaperangLab/RelaySecurityManager.swift#L760) | body | Text | 会话 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-009 | [RelaySecurityManager.swift:761](../Sources/PaperangLab/RelaySecurityManager.swift#L761) | body | Text | 历史任务 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-010 | [RelaySecurityManager.swift:762](../Sources/PaperangLab/RelaySecurityManager.swift#L762) | body | Text | 审计 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-011 | [RelaySecurityManager.swift:764](../Sources/PaperangLab/RelaySecurityManager.swift#L764) | body | TextField | 稳定用户 ID 筛选 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-012 | [RelaySecurityManager.swift:765](../Sources/PaperangLab/RelaySecurityManager.swift#L765) | body | Toggle | 隐藏已吊销 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-013 | [RelaySecurityManager.swift:766](../Sources/PaperangLab/RelaySecurityManager.swift#L766) | body | Picker | 每页 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-014 | [RelaySecurityManager.swift:770](../Sources/PaperangLab/RelaySecurityManager.swift#L770) | body | Text | Text(String($0)).tag($0) | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-015 | [RelaySecurityManager.swift:773](../Sources/PaperangLab/RelaySecurityManager.swift#L773) | body | Button | 刷新 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-016 | [RelaySecurityManager.swift:779](../Sources/PaperangLab/RelaySecurityManager.swift#L779) | body | Button | 新建凭证 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-017 | [RelaySecurityManager.swift:783](../Sources/PaperangLab/RelaySecurityManager.swift#L783) | body | Button | 生成配对私钥 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-018 | [RelaySecurityManager.swift:789](../Sources/PaperangLab/RelaySecurityManager.swift#L789) | body | Button | 登记公钥 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-019 | [RelaySecurityManager.swift:795](../Sources/PaperangLab/RelaySecurityManager.swift#L795) | body | Button | 导出历史 JSONL | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-020 | [RelaySecurityManager.swift:806](../Sources/PaperangLab/RelaySecurityManager.swift#L806) | body | Button | 上一页 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-021 | [RelaySecurityManager.swift:807](../Sources/PaperangLab/RelaySecurityManager.swift#L807) | body | Text | 第 \(model.pageNumber) 页 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-022 | [RelaySecurityManager.swift:808](../Sources/PaperangLab/RelaySecurityManager.swift#L808) | body | Button | 下一页 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-023 | [RelaySecurityManager.swift:815](../Sources/PaperangLab/RelaySecurityManager.swift#L815) | body | List | List(selection: $model.selected) | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-024 | [RelaySecurityManager.swift:823](../Sources/PaperangLab/RelaySecurityManager.swift#L823) | body | Text | name | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-025 | [RelaySecurityManager.swift:827](../Sources/PaperangLab/RelaySecurityManager.swift#L827) | body | Text | role | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-026 | [RelaySecurityManager.swift:833](../Sources/PaperangLab/RelaySecurityManager.swift#L833) | body | TextField | 新展示名称 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-027 | [RelaySecurityManager.swift:836](../Sources/PaperangLab/RelaySecurityManager.swift#L836) | body | Button | 改名 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-028 | [RelaySecurityManager.swift:843](../Sources/PaperangLab/RelaySecurityManager.swift#L843) | body | Button | 轮换 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-029 | [RelaySecurityManager.swift:850](../Sources/PaperangLab/RelaySecurityManager.swift#L850) | body | Button | 吊销 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-030 | [RelaySecurityManager.swift:858](../Sources/PaperangLab/RelaySecurityManager.swift#L858) | body | Button | 确认纸面完成 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-031 | [RelaySecurityManager.swift:865](../Sources/PaperangLab/RelaySecurityManager.swift#L865) | body | Button | 用于新凭证的现有身份 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-032 | [RelaySecurityManager.swift:876](../Sources/PaperangLab/RelaySecurityManager.swift#L876) | body | Text | 记录详情 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-033 | [RelaySecurityManager.swift:879](../Sources/PaperangLab/RelaySecurityManager.swift#L879) | body | Text | Text(model.detailText).textSelection(.enabled) | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-034 | [RelaySecurityManager.swift:888](../Sources/PaperangLab/RelaySecurityManager.swift#L888) | body | Text | Text(model.details).font(.system(.caption, design: .monospaced)).textSelection(.enabled) | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-035 | [RelaySecurityManager.swift:890](../Sources/PaperangLab/RelaySecurityManager.swift#L890) | body | Button | 清除详情 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-036 | [RelaySecurityManager.swift:903](../Sources/PaperangLab/RelaySecurityManager.swift#L903) | body | ProgressView | ProgressView().controlSize(.small) | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-037 | [RelaySecurityManager.swift:905](../Sources/PaperangLab/RelaySecurityManager.swift#L905) | body | Text | Text(model.message).font(.caption).textSelection(.enabled) | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-038 | [RelaySecurityManager.swift:906](../Sources/PaperangLab/RelaySecurityManager.swift#L906) | body | Text | C 管理与后台接收独立；十分钟锁定。列表只缓存最多12页摘要，正文/图片仅选中时读取。口令不限复杂度，空白随机生成。 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-039 | [RelaySecurityManager.swift:935](../Sources/PaperangLab/RelaySecurityManager.swift#L935) | body | Text | 新建凭证 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-040 | [RelaySecurityManager.swift:936](../Sources/PaperangLab/RelaySecurityManager.swift#L936) | body | TextField | 展示名称 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-041 | [RelaySecurityManager.swift:937](../Sources/PaperangLab/RelaySecurityManager.swift#L937) | body | Picker | 权限 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-042 | [RelaySecurityManager.swift:941](../Sources/PaperangLab/RelaySecurityManager.swift#L941) | body | Text | Text($0) | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-043 | [RelaySecurityManager.swift:943](../Sources/PaperangLab/RelaySecurityManager.swift#L943) | body | Picker | 种类 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-044 | [RelaySecurityManager.swift:945](../Sources/PaperangLab/RelaySecurityManager.swift#L945) | body | Text | 随机密钥 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-045 | [RelaySecurityManager.swift:946](../Sources/PaperangLab/RelaySecurityManager.swift#L946) | body | Text | 口令 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-046 | [RelaySecurityManager.swift:948](../Sources/PaperangLab/RelaySecurityManager.swift#L948) | body | SecureField | 自定义口令（空白随机生成） | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-047 | [RelaySecurityManager.swift:949](../Sources/PaperangLab/RelaySecurityManager.swift#L949) | body | Picker | 身份 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-048 | [RelaySecurityManager.swift:951](../Sources/PaperangLab/RelaySecurityManager.swift#L951) | body | Text | 由服务器新建身份 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-049 | [RelaySecurityManager.swift:953](../Sources/PaperangLab/RelaySecurityManager.swift#L953) | body | Text |  ·  | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-050 | [RelaySecurityManager.swift:955](../Sources/PaperangLab/RelaySecurityManager.swift#L955) | body | Text | 身份和凭证编号由服务器生成；选择现有身份可保留用量及历史关联。 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-051 | [RelaySecurityManager.swift:958](../Sources/PaperangLab/RelaySecurityManager.swift#L958) | body | Button | 取消 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-052 | [RelaySecurityManager.swift:963](../Sources/PaperangLab/RelaySecurityManager.swift#L963) | body | Button | 创建 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-053 | [RelaySecurityManager.swift:990](../Sources/PaperangLab/RelaySecurityManager.swift#L990) | quotaEditor | Picker | 对象 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-054 | [RelaySecurityManager.swift:992](../Sources/PaperangLab/RelaySecurityManager.swift#L992) | quotaEditor | Text | 匿名默认 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-055 | [RelaySecurityManager.swift:993](../Sources/PaperangLab/RelaySecurityManager.swift#L993) | quotaEditor | Text | 注册默认 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-056 | [RelaySecurityManager.swift:994](../Sources/PaperangLab/RelaySecurityManager.swift#L994) | quotaEditor | Text | 所选身份 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-057 | [RelaySecurityManager.swift:997](../Sources/PaperangLab/RelaySecurityManager.swift#L997) | quotaEditor | TextField | 每小时任务数（空白继承） | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-058 | [RelaySecurityManager.swift:998](../Sources/PaperangLab/RelaySecurityManager.swift#L998) | quotaEditor | TextField | 每小时内容量（空白继承） | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-059 | [RelaySecurityManager.swift:999](../Sources/PaperangLab/RelaySecurityManager.swift#L999) | quotaEditor | TextField | 单次内容量（空白继承） | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-060 | [RelaySecurityManager.swift:1002](../Sources/PaperangLab/RelaySecurityManager.swift#L1002) | quotaEditor | Button | 读取 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-061 | [RelaySecurityManager.swift:1008](../Sources/PaperangLab/RelaySecurityManager.swift#L1008) | quotaEditor | Button | 保存 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelaySecurityManager-062 | [RelaySecurityManager.swift:1015](../Sources/PaperangLab/RelaySecurityManager.swift#L1015) | quotaEditor | Text | 0 禁止，空白继承；内容量为文字字符+图片行数。切换对象后必须重新读取，轮换不清零。 | 模块已定位，逐项行为待核对 | AdministrationPage.cs / AdministrationTools.cs / WorkspaceFeatureChecks.cs |
| RelayView-001 | [RelayView.swift:10](../Sources/PaperangLab/RelayView.swift#L10) | relayPage | Picker | 远程打印页面 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-002 | [RelayView.swift:14](../Sources/PaperangLab/RelayView.swift#L14) | relayPage | Text | Text($0) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-003 | [RelayView.swift:37](../Sources/PaperangLab/RelayView.swift#L37) | relayPage | Button | 知道了 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-004 | [RelayView.swift:42](../Sources/PaperangLab/RelayView.swift#L42) | relayPage | Text | Text(lab.relayMessage) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-005 | [RelayView.swift:76](../Sources/PaperangLab/RelayView.swift#L76) | relayConnectionSummary | Text | 中转在线 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-006 | [RelayView.swift:77](../Sources/PaperangLab/RelayView.swift#L77) | relayConnectionSummary | Text | Text(lab.relayMessage).font(.caption).foregroundStyle(.secondary).textSelection(.enabled) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-007 | [RelayView.swift:80](../Sources/PaperangLab/RelayView.swift#L80) | relayConnectionSummary | Text | 最近响应：\(time.formatted(date: .omitted, time: .standard)) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-008 | [RelayView.swift:87](../Sources/PaperangLab/RelayView.swift#L87) | relayConnectionSummary | Button | 断开 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-009 | [RelayView.swift:90](../Sources/PaperangLab/RelayView.swift#L90) | relayConnectionSummary | Button | 连接中转 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / BuildRelay |
| RelayView-010 | [RelayView.swift:97](../Sources/PaperangLab/RelayView.swift#L97) | relayConnectionSettings | GroupBox | 服务器与身份 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-011 | [RelayView.swift:101](../Sources/PaperangLab/RelayView.swift#L101) | relayConnectionSettings | TextField | 服务器根地址 · https://print.example.com | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-012 | [RelayView.swift:102](../Sources/PaperangLab/RelayView.swift#L102) | relayConnectionSettings | Picker | 认证方式 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-013 | [RelayView.swift:104](../Sources/PaperangLab/RelayView.swift#L104) | relayConnectionSettings | Text | 所有者私钥 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-014 | [RelayView.swift:105](../Sources/PaperangLab/RelayView.swift#L105) | relayConnectionSettings | Text | M 设备密钥（兼容） | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-015 | [RelayView.swift:111](../Sources/PaperangLab/RelayView.swift#L111) | relayConnectionSettings | TextField | 所有者 JSON 文件路径 | 有对应实现，未逐控件验收 | ConnectionPage.cs / owner |
| RelayView-016 | [RelayView.swift:112](../Sources/PaperangLab/RelayView.swift#L112) | relayConnectionSettings | Button | 选择文件… | 有对应实现，未逐控件验收 | ConnectionPage.cs / owner |
| RelayView-017 | [RelayView.swift:116](../Sources/PaperangLab/RelayView.swift#L116) | relayConnectionSettings | SecureField | M 设备密钥 psr_… | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-018 | [RelayView.swift:118](../Sources/PaperangLab/RelayView.swift#L118) | relayConnectionSettings | Toggle | 允许 HTTP 连接 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-019 | [RelayView.swift:119](../Sources/PaperangLab/RelayView.swift#L119) | relayConnectionSettings | Text | 公网使用 HTTPS。连接中的服务器与身份在重新连接后应用。 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-020 | [RelayView.swift:122](../Sources/PaperangLab/RelayView.swift#L122) | relayConnectionSettings | GroupBox | 接收与重连 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-021 | [RelayView.swift:126](../Sources/PaperangLab/RelayView.swift#L126) | relayConnectionSettings | Toggle | 启动后自动连接中转 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-022 | [RelayView.swift:127](../Sources/PaperangLab/RelayView.swift#L127) | relayConnectionSettings | Toggle | 连接后默认自动打印 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-023 | [RelayView.swift:128](../Sources/PaperangLab/RelayView.swift#L128) | relayConnectionSettings | Toggle | 自动重连已选打印机 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-024 | [RelayView.swift:129](../Sources/PaperangLab/RelayView.swift#L129) | relayConnectionSettings | Toggle | 本轮重试失败后，一分钟后继续连接 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-025 | [RelayView.swift:130](../Sources/PaperangLab/RelayView.swift#L130) | relayConnectionSettings | Text | 中转每 2 秒刷新。打印机每 30 秒尝试重连；全局耗纸授权仍由侧栏控制。 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-026 | [RelayView.swift:135](../Sources/PaperangLab/RelayView.swift#L135) | relayConnectionSettings | Text | 配置自动保存在本机 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-027 | [RelayView.swift:137](../Sources/PaperangLab/RelayView.swift#L137) | relayConnectionSettings | Button | 导出配置… | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-028 | [RelayView.swift:141](../Sources/PaperangLab/RelayView.swift#L141) | relayConnectionSettings | Button | 放弃未回报结果（不会重打） | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-029 | [RelayView.swift:146](../Sources/PaperangLab/RelayView.swift#L146) | relayStyleSettings | GroupBox | 纸条样式 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-030 | [RelayView.swift:150](../Sources/PaperangLab/RelayView.swift#L150) | relayStyleSettings | Picker | 样式 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-031 | [RelayView.swift:154](../Sources/PaperangLab/RelayView.swift#L154) | relayStyleSettings | Text | Text($0) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-032 | [RelayView.swift:159](../Sources/PaperangLab/RelayView.swift#L159) | relayStyleSettings | Picker | 模板 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-033 | [RelayView.swift:163](../Sources/PaperangLab/RelayView.swift#L163) | relayStyleSettings | Text | Text($0.name).tag($0.id.uuidString) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-034 | [RelayView.swift:167](../Sources/PaperangLab/RelayView.swift#L167) | relayStyleSettings | Button | 打开图形化样式编辑器… | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-035 | [RelayView.swift:172](../Sources/PaperangLab/RelayView.swift#L172) | relayStyleSettings | integer | 打印浓度 | 有对应实现，未逐控件验收 | WorkspaceWindow.cs / density |
| RelayView-036 | [RelayView.swift:173](../Sources/PaperangLab/RelayView.swift#L173) | relayStyleSettings | integer | 批后送纸（行） | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-037 | [RelayView.swift:176](../Sources/PaperangLab/RelayView.swift#L176) | relayStyleSettings | GroupBox | 纸面附加信息 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-038 | [RelayView.swift:182](../Sources/PaperangLab/RelayView.swift#L182) | relayStyleSettings | Toggle | 正文 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-039 | [RelayView.swift:183](../Sources/PaperangLab/RelayView.swift#L183) | relayStyleSettings | Toggle | 投稿身份 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-040 | [RelayView.swift:184](../Sources/PaperangLab/RelayView.swift#L184) | relayStyleSettings | Toggle | 时间 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-041 | [RelayView.swift:185](../Sources/PaperangLab/RelayView.swift#L185) | relayStyleSettings | Toggle | 短编号 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-042 | [RelayView.swift:186](../Sources/PaperangLab/RelayView.swift#L186) | relayStyleSettings | Toggle | IP 地址 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-043 | [RelayView.swift:187](../Sources/PaperangLab/RelayView.swift#L187) | relayStyleSettings | Toggle | User Agent | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-044 | [RelayView.swift:188](../Sources/PaperangLab/RelayView.swift#L188) | relayStyleSettings | Toggle | 浏览器信息 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-045 | [RelayView.swift:191](../Sources/PaperangLab/RelayView.swift#L191) | relayStyleSettings | Text | 自定义 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-046 | [RelayView.swift:196](../Sources/PaperangLab/RelayView.swift#L196) | relayManagement | Text | 管理凭证、配额与完整历史 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-047 | [RelayView.swift:197](../Sources/PaperangLab/RelayView.swift#L197) | relayManagement | Text | 使用 C 凭证或所有者私钥登录。管理会话独立于后台接收。 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-048 | [RelayView.swift:215](../Sources/PaperangLab/RelayView.swift#L215) | relayTaskList | Text | 接收队列 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-049 | [RelayView.swift:217](../Sources/PaperangLab/RelayView.swift#L217) | relayTaskList | Toggle | 自动打印 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-050 | [RelayView.swift:221](../Sources/PaperangLab/RelayView.swift#L221) | relayTaskList | Picker | 状态 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-051 | [RelayView.swift:225](../Sources/PaperangLab/RelayView.swift#L225) | relayTaskList | Text | Text($0) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-052 | [RelayView.swift:228](../Sources/PaperangLab/RelayView.swift#L228) | relayTaskList | TextField | 搜索编号、身份或内容 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-053 | [RelayView.swift:232](../Sources/PaperangLab/RelayView.swift#L232) | relayTaskList | Label | 离线快照，任务状态尚未刷新 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-054 | [RelayView.swift:234](../Sources/PaperangLab/RelayView.swift#L234) | relayTaskList | List | List( selection: Binding<String?>( get: { | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-055 | [RelayView.swift:252](../Sources/PaperangLab/RelayView.swift#L252) | relayTaskList | Text | # | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-056 | [RelayView.swift:253](../Sources/PaperangLab/RelayView.swift#L253) | relayTaskList | Text |  ·  | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-057 | [RelayView.swift:259](../Sources/PaperangLab/RelayView.swift#L259) | relayTaskList | Text | Text(job.stateLabel).foregroundStyle(job.stateColor) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-058 | [RelayView.swift:260](../Sources/PaperangLab/RelayView.swift#L260) | relayTaskList | Text | Text(job.created, style: .time).font(.caption).foregroundStyle(.secondary) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-059 | [RelayView.swift:267](../Sources/PaperangLab/RelayView.swift#L267) | relayTaskList | Text | 完整任务历史请进入管理页面 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-060 | [RelayView.swift:288](../Sources/PaperangLab/RelayView.swift#L288) | relayTaskList | ProgressView | ProgressView().controlSize(.small) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-061 | [RelayView.swift:289](../Sources/PaperangLab/RelayView.swift#L289) | relayTaskList | Text | 正在生成预览… | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-062 | [RelayView.swift:296](../Sources/PaperangLab/RelayView.swift#L296) | relayTaskList | Text | 预览及复制正文需要 C 管理登录。打印时先认领再读取内容；不自动重打结果不明的任务。 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-063 | [RelayView.swift:300](../Sources/PaperangLab/RelayView.swift#L300) | relayTaskList | GroupBox | 任务详情 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-064 | [RelayView.swift:306](../Sources/PaperangLab/RelayView.swift#L306) | relayTaskList | Text | # | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-065 | [RelayView.swift:308](../Sources/PaperangLab/RelayView.swift#L308) | relayTaskList | Text | Text(job.stateLabel).foregroundStyle(job.stateColor) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-066 | [RelayView.swift:310](../Sources/PaperangLab/RelayView.swift#L310) | relayTaskList | Text | Text(job.created, format: .dateTime).font(.caption).foregroundStyle(.secondary) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-067 | [RelayView.swift:313](../Sources/PaperangLab/RelayView.swift#L313) | relayTaskList | Text | Text(job.text).textSelection(.enabled) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-068 | [RelayView.swift:317](../Sources/PaperangLab/RelayView.swift#L317) | relayTaskList | Text | 黑白绘图 · \(content[ | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-069 | [RelayView.swift:322](../Sources/PaperangLab/RelayView.swift#L322) | relayTaskList | Text | Text(job.detail).font(.caption).foregroundStyle(.secondary) | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-070 | [RelayView.swift:326](../Sources/PaperangLab/RelayView.swift#L326) | relayTaskList | Text | \n | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-071 | [RelayView.swift:333](../Sources/PaperangLab/RelayView.swift#L333) | relayTaskActions | Button | 生成预览 | 有对应实现，未逐控件验收 | RelayQueuePage.cs：拒绝 truncated 正文 |
| RelayView-072 | [RelayView.swift:334](../Sources/PaperangLab/RelayView.swift#L334) | relayTaskActions | Button | 打印所选任务 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| RelayView-073 | [RelayView.swift:337](../Sources/PaperangLab/RelayView.swift#L337) | relayTaskActions | Button | 拒绝 | 有对应实现，未逐控件验收 | RelayQueuePage.cs：仅待处理任务，发送前确认并刷新 |
| RelayView-074 | [RelayView.swift:339](../Sources/PaperangLab/RelayView.swift#L339) | relayTaskActions | Button | 复制到工作台 | 有对应实现，未逐控件验收 | RelayQueuePage.cs：C 权限读取完整内容后按真实模板合成点阵 |
| RelayView-075 | [RelayView.swift:342](../Sources/PaperangLab/RelayView.swift#L342) | relayTaskActions | Button | 确认纸面完成 | 模块已定位，逐项行为待核对 | ConnectionPage.cs / RelayPreferences.cs / RelayQueuePage.cs / RelayQueueChecks.cs |
| TrayMode-001 | [TrayMode.swift:129](../Sources/PaperangLab/TrayMode.swift#L129) | body | Text | Paperang Studio v | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-002 | [TrayMode.swift:130](../Sources/PaperangLab/TrayMode.swift#L130) | body | Text | 网络打印已暂停 | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-003 | [TrayMode.swift:132](../Sources/PaperangLab/TrayMode.swift#L132) | body | Text | 蓝牙： | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-004 | [TrayMode.swift:133](../Sources/PaperangLab/TrayMode.swift#L133) | body | Text | 打印： | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-005 | [TrayMode.swift:134](../Sources/PaperangLab/TrayMode.swift#L134) | body | Text | 本地 API：127.0.0.1:\(lab.apiPort) | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-006 | [TrayMode.swift:135](../Sources/PaperangLab/TrayMode.swift#L135) | body | Text | 中转：在线 | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-007 | [TrayMode.swift:138](../Sources/PaperangLab/TrayMode.swift#L138) | body | Text | 中转自动处理：开启 | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-008 | [TrayMode.swift:142](../Sources/PaperangLab/TrayMode.swift#L142) | body | Text | Text(lab.relayMessage) | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-009 | [TrayMode.swift:146](../Sources/PaperangLab/TrayMode.swift#L146) | body | Text | Text(lab.notice) | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-010 | [TrayMode.swift:150](../Sources/PaperangLab/TrayMode.swift#L150) | body | Text | 错误： | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-011 | [TrayMode.swift:154](../Sources/PaperangLab/TrayMode.swift#L154) | body | Text | 警告：中转重连失败，已断开 | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-012 | [TrayMode.swift:157](../Sources/PaperangLab/TrayMode.swift#L157) | body | Button | 恢复网络自动打印 | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-013 | [TrayMode.swift:161](../Sources/PaperangLab/TrayMode.swift#L161) | body | Button | 复制本地 API 令牌 | 存在具体差异 | LocalApiPage.cs：桌面页面可复制令牌；托盘无对应快捷项 |
| TrayMode-014 | [TrayMode.swift:162](../Sources/PaperangLab/TrayMode.swift#L162) | body | Button | 恢复主界面（退出托盘） | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-015 | [TrayMode.swift:169](../Sources/PaperangLab/TrayMode.swift#L169) | body | Text | Text(AppIdentity.copyright) | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
| TrayMode-016 | [TrayMode.swift:170](../Sources/PaperangLab/TrayMode.swift#L170) | body | Button | 检查 GitHub 更新… | 存在具体差异 | PreferencesPage.cs：有发布页入口，尚缺 WinClient 发行版本比较 |
| TrayMode-017 | [TrayMode.swift:171](../Sources/PaperangLab/TrayMode.swift#L171) | body | Button | 退出 Paperang Studio | 模块已定位，逐项行为待核对 | Program.cs / PreferencesPage.cs（仅部分托盘快捷项，详见上方缺口） |
