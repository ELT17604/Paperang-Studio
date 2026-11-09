# Stage 1 — macOS / v0.3.2

状态：macOS 实现与本阶段验收通过；提交本文后推送唯一开发分支。Windows、Ubuntu 尚未验收，不代表最终 Release 成功。

## 分支、起止提交与回滚

- 仓库：ELT17604/Paperang-Studio。
- 唯一开发分支：`xdev/032task`，从最新 `origin/main` 创建；开始时本地/远端均无此分支。
- 起始 SHA / 回滚参考点：`ada72fac6e283409a3e28577c1d6bcb257ab4eae`。
- 结束实现及测试 SHA：`d71380b537772587d9c3fac0ffb9c1e10ec6050b`。
- 本文随后单独以 `docs(release): add stage 1 handoff` 提交；最终交付 HEAD 为包含本文的该提交，使用 `git log -1 --format=%H -- xdev/032-handoff/01-macos.md` 获取其精确 SHA（文档不能包含自身提交哈希）。最终回复同时给出远端核实的完整 HEAD SHA。
- 原本机 `Paperang Studio` 检出在 main，工作区干净，远程 URL 正确。为遵守工作区写入权限，本阶段使用独立 `PaperangStudio-032` 克隆，原目录未改动。
- 不合并 main，不创建最终 Tag，不发布 Release，不升级或重启 Ubuntu。

## macOS 完成范围

本地 HTTP 服务所有业务路由、状态声明、Python/JavaScript 客户端、内置 OpenAPI、UI 路由说明及现行 Mac 使用文档已迁移 v2。旧业务分支已替换，不存在 v1 转发或兼容执行适配器；仅保留拒绝守卫和历史记录。

`Sources/ProtocolCore`、`Sources/PrintCore`、`Bluetooth.swift`、`Workspace.swift`、`Relay.swift` 与起始提交无差异：已验证 UUID、BLE 会话、数据帧、CRC、576-dot/72字节路径、密度与送纸、设备兼容逻辑不变。模板编辑、终端、管理、凭证、Relay 接收及预览实现保留。

## 最终 API v2 契约

正式索引：[`doc/API-v2-032.md`](../../doc/API-v2-032.md)。机器可读本地契约：[`doc/openapi.json`](../../doc/openapi.json)。正式可执行验证位于 `tests/`，不在临时交接目录。

- Local：仅 loopback，Bearer + Host + Origin；用途仍为本机扫描/连接/查询/渲染/打印/取消/日志。Local 与 Relay 的资源职责不同，不合并。
- Local 业务入口：GET status/devices/protocol/settings/jobs/jobs/{id}/logs/openapi；POST scan/connect/disconnect/query/render/jobs/jobs/{id}/cancel/feed，统一 `/v2` 前缀。
- Local 错误：`{error:{code,message}}`；已有 400/403/404/409/415/429/503 语义保持，新增旧 API 拒绝 410。
- v1：`/v1` 及 `/v1/*` 在认证、Origin、限流检查后统一返回 410 `api_v1_retired`，包括合法预检；无效认证仍为 403，HTTP 格式错误仍为 400。拒绝发生于 JSON/content-type 解析和业务调用之前。
- Local 任务：提交 202 `accepted`；查询使用 `printing/sent/unknown/cancelled`，中文独立于 `stateLabel`；`physicalCompletion` 恒为 unknown。只存当前会话最近30项，取消不能撤回已缓存字节，12秒 HTTP 断连不能证明打印取消，无 request_id 去重承诺，不自动重打。
- Relay：沿用 `doc/AUTH-v2.md` + `Server/api_v2.py` / `relay.py`，本阶段不改线协议。错误仍为 `{error:string,...}`；A 不读状态，匿名/B 仅自己，C 管理历史/凭证/配额/人工纸面确认，M 或设备绑定且已证明的 owner C 认领执行；claim 限制正文，request_id 在保留历史内去重。sent 不等于 confirmed。
- 模板 schema 1、content/raster1 schema 1、配置 schema 1、UserDefaults `.v1` 键、数据库迁移及物理协议均非 HTTP v1，不得批量替换或删除。

## 审计与清理

正式分类清单：`doc/API-AUDIT-v0.3.2.json`，扫描初始191个受 Git 管理文件，文本按 HTTP v1/v2、数据 schema、兼容/历史分类；新契约与测试另行审查。

已更新实际路由、示例、OpenAPI/状态值、Mac 使用/模板/图像/远程/托盘文档与 README；修正集成、技能参考和 Server README 中“Mac local API v1”的陈旧描述。老截图保留且明确标注为历史示例，未伪造新版截图。历史升级、安全、许可证、协议研究和旧发布记录保留。未发现必须删除的独立旧 API 类型文件；不为减少文件数量而删除有效共享实现。

## 版本声明与所有者更正

目标共同数值基线 `0.3.2`。遵照所有者最终更正，继续按 `doc/VERSIONING*.md` 保留架构后缀：Mac runtime `0.3.2-macclient`，显示 `Paperang Studio v0.3.2-macclient`，Info.plist 数值短版本 `0.3.2`、构建 `3201`。不继承旧 opt/fix 后缀。

Windows 应为 `0.3.2-winlite`，Relay 为 `0.3.2-linuxrelayserver`，RelaySkill 为 `0.3.2-relayskill`。共享 registry 的 targetVersion=0.3.2，其他端历史实际 runtime/version 保留，validation032=pending。不得把这些历史标签误解为新的已发布版本。

更新比较器保留架构匹配，只接受本平台标签；测试证明 `0.3.2-macclient` 高于 `0.3.1-macclient`、fix99、opt99，拒绝无架构版本及其他平台标签。契约破坏例外仅为所有者要求的本次 PATCH 移除 Local v1；长期 MINOR/fix 命名策略及历史标签不改。

## 测试命令与结果

完整步骤：`tests/README.md`；持久证据：`doc/VALIDATION-v0.3.2-macos.md`。

| 检查 | 命令/结果 |
|---|---|
| 原生构建 | `PAPERANG_WORK_DIR="$PWD/work/native" zsh scripts/build.sh`，arm64 release / PTY / app签名和严格校验通过 |
| 路由/内置规范/拒绝/权限/错误/渲染 | `python3 tests/test_local_v2.py --app 'work/native/Release/Paperang Studio.app'`，47次真实HTTP请求通过 |
| 静态契约/版本 | `python3 tests/test_contract_inventory.py`、`python3 scripts/check-versions.py`，通过 |
| Python/JS | `python3 -m compileall -q Clients tests`、`node --check Clients/paperang-client.js`，通过 |
| 隔离Relay HTTP | 安装Server/requirements.txt的Python执行 `tests/test_relay_v2.py`，3个真实HTTP测试通过 |
| 原生隔离集成 | `python3 tests/build_native_check.py`，随后上述Python执行 `tests/run_native_check.py`，通过 |
| 原生集成内容 | PKCS8/PKCS1、RSA proof/续期、C分页/配额/隐私清理、M/owner执行、模板保存恢复、配置兼容、专用Keychain、sent/unknown仅回报恢复、版本比较通过 |
| 提交检查 | `git diff --check`、敏感文件/意外删除/协议差异检查；交付前通过 |

Keychain 测试在受限文件沙箱中曾被宿主拒绝写入；改用正常宿主权限运行后通过。只操作随机测试 service，结束删除测试项；不是放宽产品凭证安全措施。

## 真实打印结果

仅两份，均通过真实 P2，用户明确回复“两张均正常”：

1. PS032-T1：中文/英文文字，1份，density60，feedAfter60；576×116、8352字节；完成发送并获纸面确认。
2. PS032-I1：带编号文字及黑条PNG，1份，density100，feedAfter60；576×101、7272字节；完成发送并获纸面确认。

物理测试时尚显示无后缀的初版 v0.3.2，随后按所有者更正修为 v0.3.2-macclient，原生/HTTP/模板集成重新验证；仅显示/版本比较变化，未重复耗纸。最终GUI已核对架构后缀，全局耗纸关闭；无不确定任务自动重印。

## 当前 Ubuntu Server 状态

线上仅只读 GET `/v2/public/status`：HTTP200，当前 runtime `0.3.1-linuxrelayserver-opt1`，接受任务；公布 authVersion2、contentSchemaVersion1、text/raster1、ownerExecution、quotaManagement、anonymousOwnStatus。开始有代理/握手超时，后成功；遵照所有者说明，一次明确连接成功即判连接测试通过。

没有修改线上源码、服务、数据库、历史、凭证、权限或 API，也未重启。未获专用线上测试身份，因此未在线上登录/提交/认领/读私有历史。认证及打印状态流程在本地隔离环境测试，不能将其冒称为线上端到端结果。公开能力未发现新不兼容；完整线上功能仍未验证。

## 服务端后续工作

本阶段不要求新增 Relay 端点或改变现有 v2；Ubuntu 阶段需独立原生部署验收，统一该组件版本并验证隔离与线上能力。不得为 Local v2 迁移引入新 BLE 或数据库协议，不得恢复 Relay v1/receipt 正文通道。任何必要服务器改动先隔离测试，再遵守该阶段授权。临时 `xdev/032-handoff/` 由 Ubuntu 阶段最终删除；正式契约、测试和持久验证记录保留。

## 未验证/限制

无当前阻断性失败。尚未执行物理断电、缺纸或缓存中取消故障注入，失败结果恢复采用隔离原生测试；线上仅公共能力探测；纸带终端和编辑器全部控件未逐一穷举。SwiftPM有native构建参数弃用警告。Windows/Ubuntu原生构建及最终Release验收不属本阶段，不宣称通过。

## Windows Stage 2 具体验收要求

1. 仅在 `xdev/032task` 继续；先核实远端 HEAD 与本文提交，保护本地修改，不新建平行正式版本分支。
2. 阅读正式契约，审计 Windows 是否仍有 HTTP v1 实际调用；既有 Relay v2 不退回v1，Local调用示例需使用v2及稳定英文state。
3. 保留Windows BLE/UUID/CRC/576-dot路径、模板schema1、凭证/所有者文件和升级配置兼容，不因清理丢功能。
4. 将Windows实际runtime/程序集/安装包/显示和registry统一为 `0.3.2-winlite`，去旧opt/fix，更新比较必须识别高于0.3.1-winlite-opt2；不得采用无架构后缀版本。更新 `scripts/check-versions.py` 的平台来源检查时保留跨平台一致性保护。
5. 在Windows原生构建并测试权限、错误、状态、模板保存恢复、预览、托盘/管理/凭证、隔离Relay及必要真实打印，明确区分模拟、隔离、线上、纸面结果；不得将本Mac成功替代Windows验收。
6. 线上操作必须使用专用测试身份/可识别任务，禁止改动当前Ubuntu服务；若旧Relay不支持行为，报告而不升级。
7. 完成Windows交接 `02-windows.md` 并推送同一分支；失败则BLOCKED，不把未通过工作交为已验收。最终合并/Tag/Release交由最后阶段，不能在Windows阶段擅自完成。
