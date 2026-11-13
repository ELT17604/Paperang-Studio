# v0.3.0 升级与发布记录

仓库：**ELT17604/Paperang-Studio** — https://github.com/ELT17604/Paperang-Studio

## 本次改变

- 全面转向中转 API v2：A 投件箱、B 本身份状态、C 完整管理、M 独立执行；关闭 v1/receipt 正文旁路。
- 稳定身份、可轮换密钥/Argon2id 口令、持久安全 Cookie、匿名随机实例注册、CSRF 和限流。
- RSA-PSS 本地固定私钥鉴权：可信公钥登记、单次挑战、十分钟 C 会话。WinLite 原生管理 UI；Mac SwiftUI 管理源代码同步。
- C 完整历史、投递 metadata、状态事件、身份筛选、流式 JSONL（含 raster1 数据）与审计。
- 执行器先认领、持久记录不确定状态，再读取受限正文；已结束任务不再向 M 开放正文。
- Windows 保留托盘、模板恢复、自动打印、自启、循环重连、DPAPI 凭证和防重印语义。修复模板身份区块误显示 senderBlock 文本。
- RelaySkill 更新 API v2；必须配置 A/B 随机密钥。A 的成功受理不可继续轮询，B 查询须使用同身份凭证。
- 服务器增加 argon2-cffi / cryptography 依赖，Ubuntu 安装器使用独立 venv。旧“完全标准库”只适用于历史版本。

## 升级顺序（先演练，再上线）

1. 固定并记录 origin/main 提交；备份旧应用、完整配置、SQLite 数据库及 WAL/SHM（优先 SQLite backup API）、反代配置和凭证文件。备份单独妥善保管。
2. 暂停接收投稿和自动认领，等待 claimed/printing 结束并回报。不要将正在打印的任务重置为 queued。迁移器发现活动 claim 会拒绝升级。
3. 停止旧服务，使用副本演练。安装 requirements.txt 到独立虚拟环境；不要让旧进程与新进程同时操作数据库。
4. 首次打开旧库会生成 .pre-v0.3.0-<时间>.bak 并添加安全表/身份字段。旧 submit→B、admin→C、device→M；旧有密钥摘要继续有效。新 CLI key-add 使用 A/B/C/M。旧具名任务映射到稳定身份；旧匿名不按 IP 猜测归属，user_id 保持空。
5. 启动 v0.3.0 服务器，验证 HTTPS、Cookie、Origin、CSRF、可信反代。所有 relay /v1 路径必须 410。旧网页刷新加载 v2；旧 receipt 失效，需重新建立身份。
6. 升级 WinLite/MacClient/RelaySkill。M 与 C 分开；先验证 M 心跳/队列，再验证 C 管理。先用模拟设备做权限测试，再手动允许纸张进行一次实机任务验证。
7. 验证轮换后旧 Cookie/密钥失效，改名保留历史快照，B 不越权，M 不能读取历史正文，C 导出不包含认证/claim 秘密。

## 回滚

协调回滚所有参与方，恢复升级前应用和数据库备份。不要把已升级库交给旧服务继续写。恢复期间关闭投稿和打印，人工检查已发送/不确定任务，避免重印。迁移后的新任务需由 C 导出保存，再由管理员决定处理；不要自动复制成新打印任务。
