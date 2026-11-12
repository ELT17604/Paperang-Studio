# ChatGPT 集成 · v0.3.0 实验性源码

**状态：ChatGPT MCP 实验暂停开发，当前版本不可用。** 仓库保留适配器源码，接入需要独立的认证 HTTPS/OAuth 网关和 ChatGPT 连接配置。本地 RelaySkill 可独立使用。


> opt1: Regression sources are archived outside the shipping repository. See [validation and reproduction](OPTIMIZATION-opt1.zh-CN.md). Historical test paths below refer to Git revision `4393ae0`.
[English](CHATGPT.md) · [中文文档目录](INDEX.zh-CN.md) · [集成源码与部署](../Integrations/ChatGPT/README.md) · [现有 RelaySkill](RELAY-SKILL.zh-CN.md)

Paperang Studio 新增一个面向 ChatGPT 的**源码级适配层**，复用现有 LinuxRelayServer **API v2**、A/B 提交权限和 raster1 schema 1。它**不是已经发布的新平台版本，也不会自动安装到 ChatGPT 账户中**。本次不修改 BLE 协议、原生客户端打印模板、Mac 本机 HTTP API v2 或 Relay API v2 合同。

## 架构与部署边界

ChatGPT → 已验证身份的 HTTPS/OAuth 网关（仓库尚未提供）→ 本地 MCP 适配器 → Relay API v2 → MacClient / WinLite（M 接收端）→ Paperang P2。

现有的 `RelaySkill` 是本地 Agent/CLI 的 Python 发送和 Pillow 图像排版工具；新增 `Integrations/ChatGPT/` 是轻量 MCP/HTTP 适配层。MCP 服务强制监听 `127.0.0.1:8188`，**不提供 OAuth 或多用户会话**。不能将裸 MCP 端口直接暴露公网；单纯添加 TLS 反代也不等于具有完整的身份验证。请通过支持 ChatGPT 接入要求的 HTTPS/OAuth 网关进行认证，仅使用个人专用的发送者身份。

## 已实现功能

- 读取服务器策略、内容能力和大小限制；公开状态**不能证明打印机实时在线**。
- 在用户明确要求后发送一份文字打印任务：`POST /v2/jobs`。
- 发送事先渲染的 `raster1` 点阵：固定 576 列、每行 72 字节、最高位优先、1 代表黑点，1–1400 行；校验 base64 长度、SHA-256 及请求体大小。
- 使用 B 级发送密钥读取本人任务状态，支持完整 UUID 和 12 位短任务号；A 级密钥不允许查状态。
- 使用任务专属 `request_id` UUID 幂等机制。不自动重发 POST；网络超时之后不得改用新 UUID 再次打印。

**暂不支持**在 MCP 服务中直接把 PNG/JPEG 转为位图、渲染字体、调整物理浓度/走纸、切换接收端模板、读取 C 管理历史或用 M 设备密钥打印。图片可以先由现有 RelaySkill 在本地渲染。接收端模板页眉页脚可能占用高度，整体仍受 1400 行限制。

## 使用和验证

请参阅[英文部署指南](../Integrations/ChatGPT/README.md)。部署前需要 Python 3.10+、兼容 v0.3.0 的 Relay、单独创建且可撤销的 A/B 随机 `psr_` 密钥，以及在线的 MacClient/WinLite。需要读取状态时推荐 B 级。最终是否能添加到 ChatGPT，仍取决于账户/工作区可用的插件接入流程。传统 GPT Actions 的兼容 OpenAPI 文件走 Relay 的 HTTPS 地址，不经过 MCP 服务。

纯软件测试命令：`python3 -m unittest discover -s Integrations/ChatGPT -p 'test_*.py' -v`。测试使用模拟 HTTP 传输，不触发真实打印。本次**未验证 Linux 实机部署、ChatGPT 端到端连接和纸面输出**。

## 安全与状态含义

`accepted` 仅指服务器受理；`sent` 仅指接收端传输完成；`confirmed` 需 C 管理员人工核实纸面。读取网页/文件、生成预览或安装插件，不代表用户已授权打印。凭据应保存在受保护的环境/密钥管理器中，不能提交至 Git、填入 URL 或发在聊天记录里，更不能使用 M/C 高权限密钥。

相关文档：[API v2 与权限](AUTH-v2.zh-CN.md) · [远程打印](REMOTE-PRINTING.zh-CN.md) · [版本规则](VERSIONING.zh-CN.md) · [功能矩阵](FEATURE-MATRIX.zh-CN.md)。

© 2026 ELT Group · [LICENSE](../LICENSE) · [NOTICE](../NOTICE) · CC BY-NC-SA 4.0。
