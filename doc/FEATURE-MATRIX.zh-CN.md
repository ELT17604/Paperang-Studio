# 版本功能实现矩阵 — 0.3.1

[完整逐项矩阵与证据](FEATURE-MATRIX.md) · [版本规则](VERSIONING.zh-CN.md) · [登记表](../VERSIONS.json)

状态含义：W=Windows 实测；T=Windows 上真实 HTTP/SQLite 验证，不代表 Linux 部署；S=源代码已同步，目标平台尚未验证；O=该分发范围之外；P=未实现/规划。不得把 S 写成已通过原生构建。

| 分发 | 版本 | 本次状态 |
|---|---|---|
| MacClient | v0.3.1-macclient-fix3 | 原生构建、SwiftUI 界面、本地所有者/C/M 联调通过；本次未重新验证蓝牙出纸 |
| LinuxRelayServer | v0.3.1-linuxrelayserver | 身份/权限/API v2/网页/迁移代码及本机安全测试完成；Linux systemd/TLS/上线待接手 |
| WinLite | v0.3.1-winlite | 原生 C 管理界面、RSA 私钥、A/B/C/M 管理、历史筛选/JSONL 导出与原托盘功能；Windows 构建和集成验证 |
| RelaySkill | v0.3.1-relayskill | A/B 密钥投递、B 状态、A 只受理、文字/图片渲染、防重复投递记录 |
| WinClient / LinuxClient | 无 | 仍为规划，不以本次登记宣称已经实现 |

通用合同：中转 API v2，旧中转 v1=410；模板 v1/raster1/BLE不变。A 不读状态、B 仅本身份状态、C 全历史/管理、M 限定设备执行。私钥基于 RSA-PSS 挑战，不是 SSH 传输。Mac 本机 API 仍为 v1。

WinLite 继续不提供完整工作台、终端或本机 API；这些是 Lite 范围差异。它现在提供完整 C 管理功能。应用级正文加密、CSV 与独立附件目录导出未实现；本版 JSONL 内含正文、metadata、事件和 raster1 base64。

过往纸面证据：2026-11-06 用户确认中文完整出纸，两次 BLE 重连。新版本本机模拟传输/实际 HTTP 验证不等于新纸面确认；未进行 Windows 实际重启、无限期稳定性。所有平台接手要求见 [handoff](HANDOFF-v0.3.0.zh-CN.md)。

Linux补充：Ubuntu 24.04/Python 3.12 的22项安全/HTTP/SQLite测试、隔离及公网Chromium HTTPS登录刷新、最大raster和重启验证通过。生产源771c14c17326已迁移并完成非root systemd冷重启、公网HTTPS、旧v1退役与部署哈希验证。原生v2接收端、真实出纸与长期无人值守仍待验收。首次安装公网探测失败的原始记录保留，后续复核通过。见[Linux验证记录](LINUX-v0.3.0-VALIDATION.md)。

## v0.3.1 correction

Anonymous browser sessions track their own fixed progress only; ordinary A tokens stay submit-only. WinLite defaults to a paired owner private file, with renewable proof sessions for C management and device-scoped execution. IP/UA are restored to active rendering metadata. Server regression: 25 tests; Windows regression: 51 checks plus native owner integration. Historical Linux v0.3.0 deployment evidence remains separate from the new owner cutover.


See [v0.3.1 live validation and browser metadata follow-up](VALIDATION-v0.3.1.zh-CN.md) for verified deployment scope and remaining platform checks.


WinLite `v0.3.1-winlite-fix1`: native management layout, automatic credential/identity IDs, hide-revoked and 25/50/100-row paging. Legacy relay compatibility is retained; server summary/filter pagination is additive. See [Windows guide](WINDOWS.md).

### 2026-11-09 / v0.3.1 revisions

| Feature | LinuxRelayServer fix1 | WinLite fix3 | MacClient fix1 |
| --- | --- | --- | --- |
| Unrestricted custom passphrases | Enforced; 39 tests | Native input updated | Source updated; native validation pending |
| Class and per-identity quotas | SQLite policy + atomic counters | Native management dialog | Source editor; native validation pending |
| Standard installer and uninstall | systemd deployment | Per-user setup, optional data purge | Existing distribution |
| Manual WinLite release check | N/A | Stable GitHub WinLite tags | N/A |

Server source ready; live deployment pending. Compatible API v2 baseline remains 0.3.1.

## MacClient fix3 验证补充

当前 Mac 原生构建、管理分页/配额/私钥续期及恢复已验证；上方 fix1 的待验标签为历史记录。服务器 39 项测试通过，未新增实机出纸或生产部署验证。[详细范围](MACCLIENT-v0.3.1.zh-CN.md)。

**实验性集成（非正式分发版本）：** [ChatGPT 适配器](CHATGPT.zh-CN.md) 复用 A/B Relay API v2，提供文字、预渲染 raster1 与 B 级本人任务查询；已提供模拟 HTTP 测试，但尚未完成 MCP/OAuth 网关部署、ChatGPT 账户连接或实机纸面验证。不增加兼容基线或正式分发版本。

## opt1

[当前源码清理、各端验证与测试归档](OPTIMIZATION-opt1.zh-CN.md)。四个已实现分发统一 opt1；不新增 Windows 原生、硬件或部署验证声明。

Linux opt1补充：sudo字符管理终端支持密钥分发/吊销、身份/配额与完整历史/导出；固定包全新重置保留一个指定Windows所有者。39项回归、4项管理后端、隔离重置/回滚及字符界面导航通过，主机全新部署及原Windows重连已验证。

字符终端补充：支持服务配置草稿、校验、原子保存、重启健康检查和失败恢复；新增密钥可归属已有身份，凭据可直接进入身份额度配置。6项配置保存/失败/并发/活动打印测试通过，新增配置页更新待应用。
