# 版本功能实现矩阵 — 0.3.1

[完整逐项矩阵与证据](FEATURE-MATRIX.md) · [版本规则](VERSIONING.zh-CN.md) · [登记表](../VERSIONS.json)

状态含义：W=Windows 实测；T=Windows 上真实 HTTP/SQLite 验证，不代表 Linux 部署；S=源代码已同步，目标平台尚未验证；O=该分发范围之外；P=未实现/规划。不得把 S 写成已通过原生构建。

| 分发 | 版本 | 本次状态 |
|---|---|---|
| MacClient | v0.3.1-macclient | SwiftUI 管理、M 认领后取正文、Keychain 凭证/待回报记录、循环重连已写；macOS 构建和蓝牙验证待接手 |
| LinuxRelayServer | v0.3.1-linuxrelayserver | 身份/权限/API v2/网页/迁移代码及本机安全测试完成；Linux systemd/TLS/上线待接手 |
| WinLite | v0.3.1-winlite | 原生 C 管理界面、RSA 私钥、A/B/C/M 管理、历史筛选/JSONL 导出与原托盘功能；Windows 构建和集成验证 |
| RelaySkill | v0.3.1-relayskill | A/B 密钥投递、B 状态、A 只受理、文字/图片渲染、防重复投递记录 |
| WinClient / LinuxClient | 无 | 仍为规划，不以本次登记宣称已经实现 |

通用合同：中转 API v2，旧中转 v1=410；模板 v1/raster1/BLE不变。A 不读状态、B 仅本身份状态、C 全历史/管理、M 限定设备执行。私钥基于 RSA-PSS 挑战，不是 SSH 传输。Mac 本机 API 仍为 v1。

WinLite 继续不提供完整工作台、终端或本机 API；这些是 Lite 范围差异。它现在提供完整 C 管理功能。应用级正文加密、CSV 与独立附件目录导出未实现；本版 JSONL 内含正文、metadata、事件和 raster1 base64。

过往纸面证据：2026-11-06 用户确认中文完整出纸，两次 BLE 重连。新版本本机模拟传输/实际 HTTP 验证不等于新纸面确认；未进行 Windows 实际重启、无限期稳定性或 Mac 原生编译。所有平台接手要求见 [handoff](HANDOFF-v0.3.0.zh-CN.md)。

Linux补充：Ubuntu 24.04/Python 3.12 的22项安全/HTTP/SQLite测试、隔离及公网Chromium HTTPS登录刷新、最大raster和重启验证通过。生产源771c14c17326已迁移并完成非root systemd冷重启、公网HTTPS、旧v1退役与部署哈希验证。原生v2接收端、真实出纸与长期无人值守仍待验收。首次安装公网探测失败的原始记录保留，后续复核通过。见[Linux验证记录](LINUX-v0.3.0-VALIDATION.md)。

## v0.3.1 correction

Anonymous browser sessions track their own fixed progress only; ordinary A tokens stay submit-only. WinLite defaults to a paired owner private file, with renewable proof sessions for C management and device-scoped execution. IP/UA are restored to active rendering metadata. Server regression: 25 tests; Windows regression: 51 checks plus native owner integration. Historical Linux v0.3.0 deployment evidence remains separate from the new owner cutover.
