# Stage 2 — Windows / v0.3.2-winlite

状态：Windows 原生实现、构建、安装升级、隔离 v2 集成及三份真实 P2 打印验收通过；线上网络存在间歇性超时，范围和未验项目明确见下文。Ubuntu 原生部署/最终发布仍待 Stage 3。本阶段未合并 main、创建 Tag 或发布 Release。

## 1. Git 信息与交付定位

- 唯一分支：`xdev/032task`，仓库 `ELT17604/Paperang-Studio`。
- 接手远端 HEAD：`51f9ec8bcc1bd6ac0b089a924541e9fc3750f780`；已确认包含 macOS 交接和实现 `d71380b537772587d9c3fac0ffb9c1e10ec6050b`。
- Windows 实现提交：`7cda10a`（版本/更新比较/DPI）。
- 完成实现、测试与持久验证文档的提交：`c5c04bf16f093957fdf878dc353b0e9f084d557c`。本地最终安装包/portable/lite 均由此干净提交重建。
- 最终交付 HEAD：随后单独提交本文的 `docs(release): add stage 2 Windows handoff`（含安装测试运行前置条件澄清），不是上面的构建源提交。与 Stage 1 相同，文档无法包含自己的提交哈希；用 `git log -1 --format=%H -- xdev/032-handoff/02-windows.md` 得到完整最终 SHA，最终回复同时报告与远端核对后的值。
- 外层目录为无提交的 Git 工作区，有用户未跟踪历史文件；没有覆盖/清理/提交它们。使用已有干净 `upstream` 仓库 fetch，切换并 fast-forward 到唯一远端工作分支，未重置 macOS 提交。
- 交付检查：所有本阶段源码/文档均提交，构建输出、测试日志、临时 owner/DB/attempt 文件不进 Git。提交后核对本地与 origin HEAD 一致；如最终推送失败不得把本文件当成已完成远端交接。最终回复给出实际推送结果。

修改文件清单见文末；还可运行 `git diff --name-only 51f9ec8..HEAD`。

## 2. Windows 完成情况

### API 与权限

独立检查实际源代码后确认原 Windows 正式业务已全部使用 Relay v2，没有 HTTP v1 执行分支需要继续删除。已加源码守卫和真实请求测试，不仅隐藏 UI。`AuthenticationMode=legacy` 是旧配置保存的 Relay-v2 M/bearer 方式，仍有迁移用途；模板/content/config schema 1、字体名及老版本证据保留，未误当成 HTTP v1 删除。

实际 `RelayClient`、`OwnerAuthentication`、`RelayEngine`、管理/配额面板和程序 owner 配对入口均使用 `/v2`。C owner 私钥绑定服务器，PKCS8/PKCS1 RSA-PSS 证明与会话续期通过；普通 C bearer 不得执行，只有已证明且设备绑定的 owner C 或 M 执行；M 不投稿、不管理历史、不确认纸面。A 投稿但不读状态，B/匿名仅自己。正式契约没有被弱化或另建 Windows 协议。

### 任务与数据安全

实际引擎领取后先持久化 unknown，再取带 claim 的正文/渲染/发送。发送完成只记 sent；重启/断线仅补报 journal，不重新取正文或重印。暂停保留在途发送，shutdown 取消留下 unknown；失联五分钟及 late sent report 只改变报告，不授权重印。C 手动纸面确认有明确用户核对对话框。取消排队 Relay 任务使用既有 rejected，不发明跨端 cancelled 状态。

DPAPI、settings/draft/previous、模板、owner 路径、M 兼容配置、原 Run 启动偏好及安装 AppId/目录均保留。安装测试时对整个用户数据目录计算摘要，前后逐字节一致；实际 installed owner 后续连接成功证明原 DPAPI 文件/私钥仍可读。无用户数据清理、密钥轮换或本地路径/真实密钥提交。

### 版本、构建和 UI 修复

runtime/display/VERSION/registry 为 `0.3.2-winlite`；两个 .NET 项目 Version=0.3.2、AssemblyVersion/FileVersion=0.3.2.0，installer 数值版本 0.3.2.0。Informational/ProductVersion 可附加标准 Git build SHA。遵守架构后缀约定；0.3.2-winlite 高于 0.3.1-winlite-fix99/opt99，拒绝其他平台/无后缀/溢出修订号。没有伪造下载地址。

修复模板编辑器缺少 96-DPI 初始基线/布局暂停的问题。本机旧布局缩小、重叠/裁切；修复后完整预览、属性、保存与取消可见，取消通过。未重写客户端、未修改 BLE/UUID/CRC/分包/576-dot/浓度/送纸实现。

构建产物（本地，未上传 Release）及 `.sha256`：

- `PaperangStudio-0.3.2-winlite-win-x64-setup.exe`：当前用户标准安装包，含 .NET 10。
- `V0.3.2-winlite-win-x64-portable.zip`：自带运行环境。
- `V0.3.2-winlite-win-x64-lite.zip`：依赖 .NET 10 Desktop Runtime。
- 两个展开目录中的 `PaperangStudio.exe`，及原生测试可执行文件。

默认产物位于仓库旁 `PaperangStudio-Windows-Development/Release/`；最终回复提供实际本地链接。构建机 Windows 11 Pro for Workstations x64 26200 / SDK10.0.403 / runtime10.0.14 / Inno6.7.3。

### 验证

正式证据：[VALIDATION-v0.3.2-windows.md](../../doc/VALIDATION-v0.3.2-windows.md)。可复现入口：[tests/windows/README.md](../../tests/windows/README.md)。

- 55 项 Windows 原生检查通过；真实 GDI/DPAPI，fake BLE/打印机覆盖介质/浓度/取消/认领/不确定结果恢复。
- 12 项实际 .NET + 隔离 Python Relay 集成检查通过：owner 两种 PEM、过期续期、C 历史/配额、执行权限、M 拒绝和 text/raster1。
- 5 组实际隔离 HTTP 测试通过：A/B/C/M、正文隔离、UUID 去重、超时 unknown、late sent、C-only confirmation、拒绝排队任务、v1=410。
- 共享 inventory/OpenAPI/version guard、Python compileall、diff/sensitive scan 通过。
- 已安装 0.3.1-winlite-opt2 -> 0.3.2-winlite、运行中覆盖升级、卸载保留全部数据/删除 Run 项、无载荷后重新安装、程序重启和托盘驻留通过；原启动偏好恢复。完整干净 Windows 用户/VM、实际注销/登录或系统重启未测试，不冒称通过。
- Inno 卸载 launcher 比实际卸载进程先退出，最初测试断言过早；测试改为等待实际清理，再次验证通过，未绕过产品错误。

### 真实打印

蓝牙发现 1 台 P2；连接/断开及两次重连通过，P2、电量100%、有纸、盖闭。三份均只发一次、每次预写 exclusive attempt 防重放，density60/feed60 仅测试内存覆盖，无用户偏好变更。

| 标识 | 路径 | 结果 |
|---|---|---|
| PS032-W-T1 | 本机 Windows -> BLE P2，576x100 中英文本 | 用户明确“两张均正常”（T1/IT1） |
| PS032-W-IT1 | 本机 Windows -> BLE P2，576x272 图片+schema1 模板 | 同上 |
| PS032-W-R1 / `6c0b6979746bab9e354867ec2b5dc311f8fd5ce4` | 隔离临时 Relay -> 实际 Windows 引擎 -> BLE P2 | 用户明确“纸面正常” |

这三份均未经过现有 Ubuntu。数据库 sent 与用户纸面确认分别记录；没有代用户往线上提交 confirmed。不确定结果没有自动重印。GUI/DPI/版本更改不影响 raster/transport，未为后续文档/布局重建重复耗纸。

## 3. 全局契约与线上状态

macOS/Windows 共同采用 Stage 1 冻结的 Relay v2、template/content/config schema1、权限与任务语义。Windows 没有 Mac loopback HTTP 服务，这是平台功能范围差异。没有修改 macOS 或服务端线协议/数据库，无需针对本阶段新增端点或迁移。共享测试只补充证据，未更改要求；Mac GUI 原生验收引用 Stage 1，未冒称 Windows 上重跑 Mac GUI。

现有 Ubuntu 源码、服务、数据库/Schema、历史、凭证/权限全部未改，也没有 reset/restart/deploy。独立 Python public status 8秒超时、两次 .NET public探测30秒超时、curl直连TLS失败；这些仍是失败探测，未写成通过。随后实际 installed WinLite 用原 owner 配置自动恢复“中转在线 · 待处理 0 项”，P2已连接。实际引擎只有在 authenticated device/status + jobs 成功后才设 Online，故这是生产 owner login/device polling/reconnect 的有效证据。

无法据此保证生产服务器所有功能都正常；网络仍间歇性不稳定，运行版本未独立取得，也未测试生产投稿/claim/打印/confirm/C历史/配额修改。完整远程纸面验证在隔离环境。Ubuntu 阶段必须完成线上健康和端到端复验，不得把本机探测超时直接当成服务端故障或为此降低客户端权限要求。

## 4. Ubuntu Stage 3 必做清单

1. 先保护本机修改，fetch 并 fast-forward 同一 `xdev/032task`，核对本 Windows 最终交付 HEAD、两个交接、实际 Diff；不能从旧 main 平行实现或强推。
2. 读取 API-v2-032、AUTH-v2、VERSIONS、两端持久验证、Server 实际代码；确认普通 C bearer 与 device-proven owner C 的区别，设备作用域、A/B own、M claim 内容隔离。
3. 统一 Server 实际 runtime 为 `0.3.2-linuxrelayserver`，配置/CLI/部署/UI/更新/打包元数据一致，比较高于0.3.1 opt/fix。RelaySkill 仍为历史 runtime 且 validation pending；按最终阶段范围单独完成 `0.3.2-relayskill` 及回归，禁止仅改 registry 伪装验证。
4. Ubuntu 原生隔离运行 Server requirements、共享5组 HTTP测试及现有服务端/安装/控制台回归；保留 v1=410、v2错误字符串格式、schema1、UUID request_id 去重、固定状态可见性和正文领取授权。不得恢复 receipt 内容通道。
5. 在隔离真实数据库副本或合成旧数据库验证安装/升级/回滚：保留用户/credential/session/已证明owner/设备绑定/历史/事件/配额使用/未决claim，任何必要迁移可回滚；本 Windows 阶段未新增 Schema，若需变更需独立证据与测试。
6. 验证 claim 五分钟超时变 unknown；同 claim late sent 仅补报、不重新获取/发送；取消排队任务 rejected、已缓存字节不可撤回；只有 C 在 sent 后明确纸面确认。不得用后台重试或重新入队绕过不确定打印保护。
7. 验证 C pagination/summary/detail/export、revoked过滤、quota get/set/继承/零配额/稳定identity轮换使用不清零；Windows 支持当前v2固定分页响应，不能当成HTTP v1旧协议删除。非owner C/其他设备 M 的执行拒绝要有测试。
8. 原生复验 systemd/权限/配置加载/控制台/安装更新回滚/服务重启后数据保留。部署前检查备份与可恢复性；只有在 Stage 3 用户授权范围内才部署，不能以本阶段打印授权推导 factory reset、全局权限改动或删除历史。
9. 针对既有生产 endpoint 排查 TLS/网络间歇失败，分清代理/本机路径与服务端问题；健康探测得到一次明确成功并记录 runtime/capabilities，再用专用测试身份/可识别任务做 submit -> claim -> actual Windows/Mac receiver -> sent -> 用户纸面核对 -> C确认。生产与隔离结果分别记录，不自动重印未知结果。
10. 重跑 shared version/inventory，保持 Mac/Windows验证已完成、Ubuntu真实验收后才改 passed；若改共享契约，独立提交并同步测试/两端影响，涉及 Mac 实现必须标记需要 macOS 原生复验。最终确认 Windows包源提交及SHA256，不能把本地未发布包写成现成GitHub下载。
11. 完成 Stage 3 交付记录、全部提交与远端HEAD核对。临时 `xdev/032-handoff/` 按第一阶段约定在最终阶段完成整理后删除，正式契约/tests/持久验证保留。合并 main/Tag/Release 必须遵从最后阶段的明确授权，本阶段没有给予这些授权。

## 5. 尚未完成与风险边界

无已复现而未修复的 Windows 原生构建/核心功能失败。生产 public 探测持续失败，但已观察到实际 owner receiver恢复；完整生产端到端验收仍未完成。干净系统VM、实际登录启动、硬件断电/缺纸/缓存取消故障注入未执行；签名/SmartScreen发布者证书未配置。Windows10/ARM64和不同蓝牙适配器未本阶段验证。这些限制不能省略成“全平台全部通过”。

## 6. 本阶段修改文件

- `README.md`
- `VERSIONS.json`
- `Windows/README.md`
- `Windows/VERSION`
- `Windows/installer/WinLite.iss`
- `Windows/src/Paperang.Core/AppVersion.cs`
- `Windows/src/Paperang.Core/Paperang.Core.csproj`
- `Windows/src/Paperang.WinLite/AssemblyInfo.cs`
- `Windows/src/Paperang.WinLite/Paperang.WinLite.csproj`
- `Windows/src/Paperang.WinLite/ReleaseUpdates.cs`
- `Windows/src/Paperang.WinLite/TemplateEditor.cs`
- `doc/API-v2-032.md`
- `doc/README.zh-CN.md`
- `doc/VALIDATION-v0.3.2-windows.md`
- `doc/WINDOWS-RELEASE-ARTIFACTS.zh-CN.md`
- `doc/WINDOWS.md`
- `doc/WINDOWS.zh-CN.md`
- `scripts/check-versions.py`
- `tests/README.md`
- `tests/test_contract_inventory.py`
- `tests/test_relay_v2.py`
- `tests/windows/Paperang.Tests/Paperang.Tests.csproj`
- `tests/windows/Paperang.Tests/Program.cs`
- `tests/windows/README.md`
- `tests/windows/run_integration.py`
- `tests/windows/test_installer.py`
- `xdev/032-handoff/02-windows.md`（本文，最后单独提交）。

## 7. 最终本地构建 SHA256

源提交 `c5c04bf16f093957fdf878dc353b0e9f084d557c`；仅后续交接/验证文档改变，不影响此构建源码。

| 产物 | bytes | SHA256 |
|---|---:|---|
| `PaperangStudio-0.3.2-winlite-win-x64-setup.exe` | 39110430 | `ef5a6a53e5e9651a9f5e408d8e5a196cffb34c9ad9ceb6a29002e2d66f308e9c` |
| `V0.3.2-winlite-win-x64-portable.zip` | 54134837 | `8a5fa494f1dab14a6aceade284b9fee391cc5cc88a4681df693c7748e7e01477` |
| `V0.3.2-winlite-win-x64-lite.zip` | 6850639 | `30e89cccdf174158f9d5c30f959fd193c20b8fe00b5e186a428b518e3c4c1563` |
