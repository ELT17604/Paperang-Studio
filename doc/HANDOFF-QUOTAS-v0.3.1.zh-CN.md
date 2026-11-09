# Relay / macOS 升级交接

仓库：[ELT17604/Paperang-Studio](https://github.com/ELT17604/Paperang-Studio)。以包含本文件的主分支提交为准，先核实 diff，勿覆盖线上机器的配置或数据。

## Linux server agent prompt

请部署 v0.3.1-linuxrelayserver-fix1。先备份当前服务代码、配置和 SQLite（使用 SQLite backup API 或停服一致性备份），保留所有公钥、会话及现有身份。阅读 `Server/security.py`、`api_v2.py`、`relay.py`、网页变更及 AUTH-v2 文档。在隔离数据目录运行全部 39 项服务器测试。按当前 systemd ExecStart 的路径更新程序文件及网页静态资源，保留部署配置与 data，重启原服务。新增 quota_overrides 是兼容的增量表。不要运行 owner-cutover/upgrade_owner.py，不要重新配对、吊销密钥、恢复旧数据库或修改 execution_paused。

验证 public/status 的 quotaManagement、自定义短口令、C-only 配额接口、匿名只能查自己状态且不返回正文。使用临时身份验证 0 禁止、单身份覆盖、轮换不重置、相同 request_id 不重复计费与并发不超额；清理测试身份，不调整正式用户限额。检查现有 WinLite owner 仍能连接及队列状态。保留回滚代码，但回滚时先评估新数据库兼容性；不要用旧备份覆盖新增业务记录。记录部署提交和测试结果。Windows 侧尚未部署此线上服务，不能将本地测试视作部署完成。

## macOS agent prompt

请阅读 v0.3.1-macclient-fix1 的 SwiftUI 管理源代码，原生构建并验证不限长度复杂度口令、匿名/注册默认及所选稳定身份配额读取/保存、0 与继承语义、C 权限和错误提示。目标切换时避免误保存旧目标限额。保持现有模板、钥匙串/owner 文件和 API v2 兼容性；按新 FEATURE-MATRIX 记录实际验证范围。请截取真实 macOS 窗口加入 README 美化，与现有 Windows 截图保持多平台布局。不要上传私人凭证、任务正文或用户信息。
