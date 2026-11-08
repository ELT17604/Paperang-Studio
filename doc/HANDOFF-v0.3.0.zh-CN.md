# v0.3.0 跨平台 Agent 接手 Prompt

两端使用同一仓库，未重命名：**ELT17604/Paperang-Studio**，https://github.com/ELT17604/Paperang-Studio 。最新入口 main：https://github.com/ELT17604/Paperang-Studio/tree/main 。先 fetch 并记录实际 SHA，不依赖旧聊天的代码副本。不要把 Windows 工作目录名当成新远程仓库名。

## 给 macOS Agent（完整复制）

你接手 Paperang Studio v0.3.0 MacClient 原生验证与修复。仓库 ELT17604/Paperang-Studio：https://github.com/ELT17604/Paperang-Studio，目标分发 v0.3.0-macclient。Windows agent 已同步修改全平台源码，但没有 macOS 环境，不能把“代码已写”当成“Mac 已验证”。

先 git fetch origin，记录 origin/main SHA，建立 codex/ 前缀分支，保留用户已有修改。完整阅读 README.md、doc/README.zh-CN.md、VERSIONS.json、doc/AUTH-v2.md、doc/AUTH-v2.zh-CN.md、doc/UPGRADE-v0.3.0.zh-CN.md、doc/FEATURE-MATRIX.md、doc/VERSIONING.md，以及相关 Swift/Server 源码。不要使用旧 v0.2.3 构建产物或恢复旧鉴权旁路。

新合同：中转 /v2，旧 /v1 返回 410；Mac 本地 HTTP /v1 不变。A 只投递，B 只查同 user_id 状态，C 完整管理，M 独立设备执行。网页只存安全 Cookie 和非秘密浏览器/任务 ID。完整正文/投递 metadata 只在 C 管理历史中开放；M 只能认领后携带 X-Job-Claim 读取活动正文，任务结束后不可读取。sent 不等于纸面确认，unknown 不自动重印。

重点审核/构建 Sources/PaperangLab/Relay.swift、RelayKeys.swift、RelaySecurityManager.swift、RelayView.swift、TrayMode.swift、Bluetooth.swift，确保 Swift 6 actor 隔离、SwiftUI 视图构造/选中项、Security.framework RSA PEM 导入、PKCS8→PKCS1 DER 解析正确。管理面板必须能以 C 密钥或完整口令登录；生成/导入 Owner JSON（server,id,privateKeyPem），登记公钥，RSA-PSS SHA256/MGF1 SHA256/salt32 挑战签名，十分钟会话过期后重新验证。Windows 生成 PKCS8，Mac 生成 PKCS1，双方均应能读取对方未加密 PEM 格式。私钥只存本地，权限 0600，不记录日志/不提交 Git。注册公钥不等于激活，必须证明持有权。

完整验证：凭证 A/B/C/M 创建、名称快照、轮换保留 user_id 且旧会话失效、吊销、会话清除、公钥吊销、最后 C 保护、按用户分页历史/JSONL 导出（含图片、投递信息、不含秘密）、纸面确认。自动打印顺序必须 claim→本地不确定记录→获取正文→校验/渲染→物理发送→持久结果→回报。特别检查本次新增 RelayVault.swift 的 Keychain 凭证与 journal、恢复和持久化失败处理；启动先回报 unknown，断网/退出/崩溃绝不自动二次打印；不要以 Windows 已验证为由跳过 Mac 这项审查。C 历史不应与 M 队列混用。预览/复制到工作台的正文必须用 C；已有缓存不得绕过 C 权限或在锁定后继续暴露。

运行 scripts/build.sh、签名验证、原有 Swift/本地 API 相关检查。在本机隔离 v0.3.0 relay 测试上述边界，使用临时凭证；只有用户已授权纸张时才实机打印。验证中文文字和 raster1、模板、托盘自动打印、重连、断电/断网不重印。不要将实际机器的私钥、密钥、IP/Bluetooth 地址或任务正文放入提交。

修复发现的问题，更新双语文档、发布记录、矩阵及目标平台验证证据，说明与 WinLite 的功能差异。0.3.0 仍是共同基线；不要自行更改合同，若必须改 API 同步协调 Linux/Windows。Mac 独立兼容修复可使用 -fix1（先确认仓库是否已存在）。保留 LICENSE/NOTICE/icon，构建输出在源码树外。提交改动和原生验证结果；部署/发布按用户既有授权范围执行，未获生产授权不要替换服务器。

README 与截图任务也属于本次接手范围：目前主页已调整为 MacClient、WinLite、LinuxRelayServer、RelaySkill 协作的多平台入口，保留这种整体定位，不要重新写成 macOS only。在 Mac 原生构建与功能验证通过后，实际打开对应版本的应用，使用公开的示例纸条/图片，截取至少两张有代表性的窗口：完整打印工作台（文字或图片与最终黑白预览）、模板编辑器；条件允许再补菜单栏托盘或 C 管理面板。必须是当前版本真实运行截图，不使用 AI 生成、网页仿制界面或旧版本画面冒充验证。截图保存到 assets/screenshots/，采用 macclient-v0.3.0-<view>.png 或 .jpg 命名，并在 assets/screenshots/README.md 记录版本、平台、拍摄上下文和演示数据。避免出现真实凭证、私钥、服务器地址、IP、用户任务正文、原始蓝牙标识和无关桌面内容。保持原生窗口清晰、边界完整，适度控制尺寸和文件体积，不在图片上伪造状态。同步更新根 README.md 和 doc/README.zh-CN.md，在现有 Windows 截图旁新增 macOS 实际界面展示，使用相对路径、描述性 alt 和中英文图注；按阅读顺序精选截图，不堆砌长图，不删除现有 Windows 介绍与图片。检查 GitHub 渲染、移动端宽度、图片链接与浅深色阅读效果。若某界面尚未验证或截图未能取得，明确保留待补状态，不能宣称已验证。

最终报告：仓库链接、提交 SHA/分支、Mac 构建下载路径/哈希、测试与实际纸面结果、未解决问题、是否满足矩阵。不能声称未执行的 Linux 或 Windows 回归已通过。

## 给 Linux Server Agent（完整复制）

你接手 Paperang Studio v0.3.0 LinuxRelayServer 原生验证、迁移演练及部署准备。仓库 ELT17604/Paperang-Studio：https://github.com/ELT17604/Paperang-Studio，main：https://github.com/ELT17604/Paperang-Studio/tree/main，分发 v0.3.0-linuxrelayserver。先 fetch 固定 origin/main SHA，建立 codex/ 分支并保留用户修改。Windows agent 已运行本机 HTTP/SQLite 安全回归和 WinLite v2 集成，但未运行 Linux systemd/TLS/生产迁移。

先阅读 README、VERSIONS.json、doc/AUTH-v2.md、doc/AUTH-v2.zh-CN.md、doc/UPGRADE-v0.3.0.zh-CN.md、doc/FEATURE-MATRIX.md 和 Server/ 全部源码/启动器/安装器。新依赖 Server/requirements.txt：argon2-cffi、cryptography，使用独立 venv；不能再声称纯标准库。审核 Server/security.py、api_v2.py、relay.py、install.sh、run.py、run-temporary.sh 和网页。

必须保持边界：A 只接收受理及 ID，状态 403；B 只查同 user_id 固定状态字段；C 管理全部身份/凭证/会话/公钥/完整历史及导出；M 只看所属设备待处理描述符，认领后 X-Job-Claim 获取活动正文，完成后禁止内容访问。所有旧 relay /v1 路由 410，任何 receipt/shortID 不授予正文权限。公共 status 无打印遥测。Mac 本地 /v1 是另一个 API，不要误改。

验证口令 Argon2id、随机密钥 SHA-256、稳定身份与快照、轮换/吊销即时生效、pending 公钥必须证明持有权、RSA-PSS salt32 挑战六十秒消费一次、C 会话十分钟、最后有效管理员保护。Cookie Secure/HttpOnly/SameSite Strict，普通身份最长180天；cookie POST 必须 Origin+CSRF；原生登录仅在 X-Paperang-Native=1 且无 Origin 时返回 JSON token。配置反代真实 Host 和可信 X-Forwarded-For，不盲信公网转发头。匿名随机 psb_ 实例完成注册/me 后才能投递；这是浏览器实例身份，不是可信硬件/自然人身份。

运行 python -m unittest discover -s Server -p 'test_*.py'，语法检查、隔离 HTTPS 浏览器持久登录/重载/吊销演练、A/B/M 越权、旧 receipt 旁路、跨源/CSRF、挑战重放/过期/错误签名、限流/队列满、并发重复 request_id、重启、保留期清理、长文本和最大 raster1、分页导出无遗漏/不泄露秘密。JSONL 必须包含任务正文/图片/投递信息/事件，但不含认证 token、hash、claim、receipt。检查受限进程数、SQLite锁/超时、请求体/响应大小、内存和长时间无人值守资源。完善发现的问题，新增有价值回归测试，避免只验证实现本身。

先在旧数据库副本演练迁移：暂停投稿并排空 claimed/printing，停旧服务，备份数据库/WAL/配置/凭证，使用 SQLite backup；迁移自动产生 pre-v0.3.0 备份。旧 submit→B、admin→C、device→M，保留摘要；旧具名任务映射，旧匿名不按 IP 归人。新旧服务不可并发写同库。回滚必须同时恢复旧服务与旧库，不可让旧二进制写已升级库。不要自动重置或重印不确定任务。保留新任务的安全导出供人工处理。

原生验证 install.sh --dry-run、Ubuntu系统依赖 python3-venv、文件权限、venv依赖、systemd User/WorkingDirectory/ExecStart/Restart、冷启动/重启/代理超时/公网HTTPS、备份位置/空间及权限。检查运行时是否意外写入受保护源码目录；Linux 文件访问须符合非 root 服务用户。先提交部署准备与迁移演练结果；只有用户明确授权该主机部署时才执行生产变更，否则不能将本 prompt 当成任意生产操作授权。

与 macOS/WinLite agent 对同一 v2 合同做交叉测试，更新 README/升级日志/矩阵/验证记录。保留 LICENSE/NOTICE，不提交生产配置/数据库/密钥/任务内容。最终给仓库与提交链接、命令和证据、迁移/回滚结果、运行服务版本、待解决问题。不要把 Windows 本机验证冒称 Linux 已部署。
