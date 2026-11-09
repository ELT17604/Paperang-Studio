# 变更记录

## `v0.3.1-<distribution>-opt1` — 2026-11-09

- Remove unused Mac management state/cache, unreachable retired relay handlers and redundant preference writes; move regression sources outside the shipping tree.
- Group Mac remote printing into tasks/connection/style/management; fix preview/active-job refresh and improve responsive web identity/status UI.
- Support optN update tags; restore macOS CJK font discovery in RelaySkill. Preserve authentication and no-replay boundaries.
- [Review and validation / 审阅与验证](OPTIMIZATION-opt1.zh-CN.md). Native Windows/hardware/deployment validation not claimed.

## 未发布 · ChatGPT 发送端实验性集成

- 新增仅监听本机的 MCP 发送端适配器与传统 GPT Actions OpenAPI 兼容定义，保持中转 API v2。
- 支持明确授权的文字、预渲染 raster1、服务器核实 A/B 身份、B 本身份状态与 UUID 幂等；不自动重试不确定 POST。
- 补充模拟 HTTP 合同测试、中英文文档及认证/部署边界。OAuth 网关、ChatGPT 真实连接和纸面测试均待完成，不增加正式发行版或修改共享协议。

## v0.3.1-macclient-fix2 — 2026-11-09

- macOS 应用菜单和托盘加入手动检查 GitHub 更新，只比较最近 100 条中的正式 MacClient 标签，发现新版后打开下载页面。无发行版、网络失败均提示；不自动安装。按要求未运行 macOS 验证。

## v0.3.1-winlite-fix3 / linuxrelayserver-fix1 / macclient-fix1 — 2026-11-09

- Windows 标准按用户安装包，内置 .NET、系统卸载入口和可选数据清除，托盘手动检查 GitHub 正式 WinLite 发行版。
- 自定义口令移除长度/复杂度规则，留空自动生成；保留传输边界和登录限流。
- C 管理匿名/注册默认及单身份的小时任务数、内容量和单次大小限额；SQLite 持久化与原子计数，轮换不清零。网页、WinLite 界面及 Swift 管理源代码同步。
- Windows 61 项、服务器 39 项测试通过。macOS 原生编译与线上服务器部署待验证，见交接文档。


## v0.3.1 — 2026-11-08

修复匿名用户仅查自己任务进度、活动打印任务 IP/UA 字段；WinLite 默认仅使用配对所有者私钥并自动续期。合入 Linux 安全修正，增加先验新私钥、再撤销旧凭证的切换工具。

## v0.3.0 — 2026-11-08

中转 API v2，A/B/C/M 分级、稳定身份、Argon2id 口令、持久安全 Cookie、RSA 私钥挑战、WinLite/SwiftUI 原生 C 管理和筛选历史 JSONL 导出；同步执行器、Keychain/DPAPI 待回报恢复与 RelaySkill。旧中转 v1 关闭，本地 Mac v1 不变。Windows 已测试，Mac/Linux 原生验证待接手。[升级记录](UPGRADE-v0.3.0.zh-CN.md)。

## v0.2.3-relayskill — 2026-11-08（独立技能发行版）

- 新增平行发行版 `Skills/paperang-relay-print`：文字/点阵提交、中文排版、图片变换/抖动、预览与有界回执追踪。
- 复用中转 HTTP v1，明确 Content-Length、能力/配额预检、份数及独占提交记录，不自动重发结果不明的请求。
- 投稿与设备凭证隔离，明确浓度/送纸/模板由接收客户端控制；无需修改客户端或服务器协议。
- 更新版本规则、登记表、功能矩阵和双语文档；验证 12 项检查、原中转/SQLite 联调及真实服务只读状态。

## v0.2.3-winlite-fix1 — 2026-11-08（Windows 客户端）

- 托盘固定“开启自动打印”，仅用左侧勾选表示状态；设置显示实时状态，连接页提供保存并连接。
- 自动保存模板/样式与打印选项，关闭设置刷新落盘，启动恢复，已开始的任务保持原版式快照。
- 默认持续重连：五次重试失败后等待 60 秒，再尝试五次并循环；断开/退出立即取消等待，可关闭此选项恢复原停止策略。
- 升级时修复已启用的登录启动路径，重复后台启动不弹设置；串行原子写入并合并界面刷新。
- 增加持久化/迁移、重连/取消、并发和 1200 次离线检查；短期验证不等于无限期稳定或真实重启验证。
- 新增发行版版本命名、版本登记表和功能实现矩阵；保留 Mac/服务器运行时版本作为历史兼容别名。

## 0.2.3-win_lite — 2026-11-06（Windows 客户端）

- 新增原生 Windows 托盘收纸条、设置、加密凭证及未回报结果恢复、可选登录自启与记住的打印机重连。原 macOS 默认行为、服务端和投稿网页不变。
- 新增兼容 Mac v1 JSON 的十种区块与四套预设模板编辑器，支持拖动排序、纸面选区块、字体/边距/留白、变量、导入导出及图文统一合成。
- 修复当前 Windows/P2 组合中 UUID 筛选返回空结果的问题，改为完整枚举服务后选择 FF00；保留 FF03 流控、CRC 握手、纸仓检查和浓度回读。
- 用户已确认中文测试纸完整出纸，验证 150 行送纸、连续两次重连、39 项自动检查和原服务端联调。记录其他适配器/固件及平台字体差异的验证边界，构建产物与凭证不提交源码。

## 0.2.3-fix1 — 2026-11-04

- 修复模板序列化不稳定导致远程任务预览被清空；按需获取完整内容并展示加载和错误。
- 仅更新客户端；服务端、网页和 SDK 保持原版本。

- 修复图片绕过自定义模板：正文区块合成图片和说明，其他区块按原设置渲染；编辑器与打印共用区块。兼容原文字排版和模板 JSON。

## 0.2.3 — 2026-11-04

- 新增菜单栏托盘，保持蓝牙和网络、自动授权网络任务，暂停其他后台工作并恢复原设置。
- App、网页、服务端、SDK 和当前文档统一 v0.2.3。

- 修复旧查询覆盖新投稿与 unknown 阶段退回排队。
- 新增版本化 raster1 绘图、触控画布、能力协商、行数配额、私有完整内容获取和原生预览。
- [内容 API 与限制](RICH-MEDIA.zh-CN.md)。

## 0.2.2 — 2026-11-04

- 每 2 秒心跳与状态刷新，失败重连 5 次后断开并警告，过期状态明确标识。
- 服务端任务闭环，区分数据已发送和人工确认纸面完成。
- 原生管理员密钥新建、改名、吊销与删除。
- 流控下减少发送等待，默认 BLE 10 ms、图像帧 0 ms。
- 详见[远程打印文档](REMOTE-PRINTING.zh-CN.md)。


### 0.2.2 同批包含的远程打印功能

- macOS专用终端临时体验文件夹、Python3.10+自动查找、可双击入口及完整服务端参数表。

- 任务列表/详情/状态筛选、40位内部ID与独立唯一12位可选打印短编号，兼容旧任务。
- 可保存默认自动打印偏好，图形化区块编辑器、实时合成与JSON模板导入导出。
- 持久systemd安装与临时前台体验（Ctrl-C/关闭终端退出）。
- 蓝牙扫描取消旧计时器，区分广播/历史记录，显示计数并提供App内部重建恢复。


- 新增Ubuntu中转安装脚本、端口/匿名长度/每小时IP配额、多角色密钥及离线生成导入。
- 新增原生远程打印收件箱、设备上报、四种版式与字段选择、手动/自动打印及不确定结果保护。

[English](../CHANGELOG.md) · [中文文档目录](INDEX.zh-CN.md)

## 0.2.1 — 2026-11-04

- 侧栏唯一全局耗纸开关、左下角版权与版本；文件→关于含英文声明及联系邮箱。
- CC BY-NC-SA4.0非商业署名许可和完整NOTICE。

- 默认筛选Paperang候选，保留展示全部蓝牙设备开关。
- 工程日志最新在前，导出顺序保持正序。
- 真实controlling PTY、交互zsh、控制键和分批纸带打印；隐私输入与输出上限。
- 本机HTTP v1 API、Bearer/Origin校验、离线渲染、任务/送纸与示例客户端。
- 模块化Swift库、OpenAPI、完整协议与获取过程文档。
- ESP32模拟器扩展命令研究目录，区别模拟数据与真实P2能力。
- 新Logo与ICNS/ICO；干净源码项目和独立开发档案。

## 0.2

原生用户工作台、图片渲染预览、参数预设与打印任务；用户确认图片方向/内容完整、150行送纸合适。

## 0.1

传统P2 BLE握手、只读字段、原始图像与送纸实物验证，工程协议工具。

Linux Relay v0.3.0补充：修复原始密钥Cookie旁路及浏览器公钥会话返回；旧v1先退役；事件按状态转换记录；完成Ubuntu生产迁移、冷重启及公网HTTPS复核，原生接收端/纸面/长期稳定性仍待验证。

Linux opt1新增sudo字符管理终端及固定包全新部署工具，保留指定Windows公钥身份；不监听管理HTTP端口、不增加永久管理密钥。验收成功后按明确请求清理旧用户、任务、日志、备份和旧版本。
