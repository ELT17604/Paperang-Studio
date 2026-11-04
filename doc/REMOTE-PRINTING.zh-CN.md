# 远程打印与 Ubuntu 中转安装

[English](REMOTE-PRINTING.md) · [中文文档目录](INDEX.zh-CN.md)

网页投稿 → Ubuntu轻量服务/SQLite收件箱 → Mac主动向外连接、上报状态、获取任务 → 本地选择版式并通过P2打印。Mac不需要开放公网端口。第一版支持文字投稿。

## macOS专用临时体验

新增`Server/macOS-Temporary/`，可从终端运行run.sh或双击start.command。[macOS中文说明](MACOS-TEMPORARY.zh-CN.md)列出所有可附加参数及示例；[服务端参数总表](../Server/README.md#available-parameters)区分共享参数与Ubuntu专用参数。

## 两种启动方式

持久安装：`sudo bash Server/install.sh`，由systemd管理，关闭终端仍继续服务，数据库/密钥保留。

```sh
sudo systemctl start paperang-relay
sudo systemctl stop paperang-relay
sudo systemctl restart paperang-relay
sudo systemctl status paperang-relay
sudo systemctl enable paperang-relay
sudo systemctl disable paperang-relay
```

临时体验：无需sudo，不注册服务，数据库和密钥仅临时存在：

```sh
bash Server/run-temporary.sh --port 9088
# 需要从其他电脑测试时可加 --bind 0.0.0.0
```

临时模式同样支持匿名/鉴权长度和每小时IP额度、代理CIDR、设备ID。终端会显示临时设备和投稿密钥；Ctrl-C或正常关闭终端（SIGHUP）即停止，也处理SIGTERM。正常退出清理临时数据，强制kill可能残留系统临时目录，但没有后台守护进程。`--dry-run`只展示配置，不监听端口。

## 安装和参数

在Ubuntu22.04+、Python3.10+、systemd环境，将项目放到服务器后运行：

```sh
sudo bash Server/install.sh
```

安装过程可输入监听地址、端口、匿名单条长度、每IP每小时条数/字符总量、鉴权用户额度和设备ID。也可直接指定：

```sh
sudo bash Server/install.sh --non-interactive --port 8177 --bind 127.0.0.1 \
  --anonymous-max-length 500 --anonymous-hourly-limit 5 \
  --anonymous-hourly-chars 2000 --authenticated-max-length 10000 \
  --authenticated-hourly-limit 60 --authenticated-hourly-chars 60000 \
  --device-id p2 --trusted-proxy 127.0.0.1/32
```

`--dry-run`仅校验/预演，不修改系统；`--help`查看完整参数。`--disable-anonymous`关闭匿名。`--prefix`、`--config`、`--state-dir`自定义程序/配置/数据路径；`--no-start`只安装不启用服务。重复安装保留配置/密钥，修改已有配置需`--reconfigure`，会保存旧配置备份。

默认仅监听127.0.0.1:8177，程序在`/opt/paperang-relay`，配置在`/etc/paperang-relay/config.json`，数据库在`/var/lib/paperang-relay`，服务名`paperang-relay`。不安装pip依赖，不修改防火墙/DNS/反向代理。对外推荐在现有HTTPS反向代理后转发；直接HTTP测试可指定`--bind 0.0.0.0`，Mac需开启HTTP测试选项。

```sh
sudo systemctl status paperang-relay
sudo journalctl -u paperang-relay
sudo systemctl restart paperang-relay
```

## 每小时限制

匿名默认每条500字符、每IP每小时5条/2000字符；鉴权默认每条10000字符、每IP每小时60条/60000字符。按UTC整点小时重置，例如12:00–12:59:59在13:00清零，不是滚动60分钟。配额写入SQLite，重启不会刷新；更换投稿密钥也不会刷新IP计数。匿名/鉴权共享同一IP的已接受使用量，按当前请求身份使用相应上限。同一公网IP下的用户会共享额度，仅成功接收的任务计数。

转发IP只在直接连接来自`--trusted-proxy`网段时采纳，从X-Forwarded-For右侧逐级回溯到首个非可信地址；不信任访客伪造的最左地址。默认不信任任何代理。配置真实代理CIDR，不要直接信任全网。HTTPS nginx例子见[英文完整文档](REMOTE-PRINTING.md#https-and-client-addresses)。

## 多密钥与本地生成

首次安装在`/var/lib/paperang-relay/initial-credentials.jsonl`生成三类密钥，文件仅root可读，数据库只保存哈希：

- admin：创建/列出/撤销密钥；不要分发给访客。
- device：Mac连接、状态上报、该设备收件箱与任务回报。
- submit：某位投稿人的具名身份，可独立撤销；也允许无密钥匿名投稿。

在服务器终端本地新增Alice投稿密钥：

```sh
sudo -u paperang-relay python3 /opt/paperang-relay/relay.py \
  --config /etc/paperang-relay/config.json key-add --role submit --name Alice --device p2
sudo -u paperang-relay python3 /opt/paperang-relay/relay.py \
  --config /etc/paperang-relay/config.json key-list
sudo -u paperang-relay python3 /opt/paperang-relay/relay.py \
  --config /etc/paperang-relay/config.json key-revoke KEY_ID
```

也可在Mac离线生成私密文件，再通过已有SSH通道传到服务器导入：

```sh
python3 Server/keygen.py --name Alice --role submit --device p2 \
  --out ../PaperangStudio-Development/RelayKeys/alice.keys.json
# 在Ubuntu，保证服务用户可读取该私密文件：
sudo -u paperang-relay python3 /opt/paperang-relay/relay.py \
  --config /etc/paperang-relay/config.json key-import --file /private/path/alice.keys.json
```

生成器使用0600权限、不覆盖文件、不在输出中显示密钥。只有导入到该中转后密钥才生效；重复密钥被拒绝。导入逐条处理，完成后可移除传输副本。可生成多个不同名字/角色/设备的密钥，原文只在创建时给出，列表不返回原文。

## Mac远程打印页面

进入“远程打印”，填服务器根地址和device密钥，点击连接；密钥仅留在本次运行内存中。每2秒上报当前连接状态和已经采样的设备字段，并刷新收件箱。公网状态页面只显示在线/打印机连接/忙碌/最后上报时间；设备密钥可查看任务正文和来源。

本地选择“便签/小票/复古电传/访客纪念”四种风格，以及正文、身份、时间、IP、UA和浏览器设备字段、浓度和送纸。IP/UA/浏览器字段默认不印。先预览，再确认打印或拒绝；默认人工处理，可手动开启自动打印，但始终服从侧栏唯一全局耗纸开关。过长版式超过1400行会停止提交，应换小字号样式或拒绝。

网页会告知投稿人：服务器IP、UA、语言、时间、浏览器上报的平台/时区/屏幕等随任务发送，并可能被印到纸上。浏览器字段和UA不等于已验证的精确硬件身份；不请求定位。网页不把鉴权密钥保存在浏览器存储。

## 队列和部署边界

任务为queued→claimed→printing→sent，或rejected/unknown；认领是原子操作，另有独立claim凭据。另一设备密钥不能伪造该认领的结果。打印中持续更新状态；超过5分钟未回报的认领任务在清理时标为unknown，不自动重打。已发送仅代表传输结束，不能当作纸面确认。断线、App退出或结果回报失败时应核对纸面；“放弃未回报结果”只清除本地关联，不重印旧任务。

默认最多200个活动任务、历史保留7天，正常请求时清理。API正文64KiB，最多32个连接，套接字超时12秒。服务器API与本机loopbackAPI是两套接口；完整路由/权限/错误表见[英文API说明](REMOTE-PRINTING.md#relay-api)。

本地已验证并发额度、UTC整点重置、重启保留配额、密钥/角色/设备隔离、原子认领、状态与代理IP，并构建Mac界面。实际Ubuntu/systemd安装、生产HTTPS和新增实物打印需要在目标环境验证。

## 任务列表与样式编辑

现在保留活动/近期任务及状态，点击加载全文详情。40位内部ID用于追踪；独立唯一的12位十六进制短编号供可选打印，旧UUID任务自动补短编号。可保存“下次连接默认自动打印”偏好。新增可排序/拖动区块、字体/字号/对齐/留白、JSON导入导出的图形化样式编辑器。完整操作见[任务与样式指南](LAYOUT-EDITOR.zh-CN.md)。

## 0.2.3 — 连接健康、任务反馈与密钥管理

客户端每 2 秒执行一轮已鉴权的状态上报和任务刷新，单次请求 4 秒超时。首次失败立即显示不可用，之后最多重试 5 次，每次间隔 2 秒；成功后重置次数。五次重连均失败则断开、关闭自动打印并弹出警告。显示最近响应时间，断线时明确标注任务列表为历史快照；旧连接响应不会覆盖新连接。结果不确定的纸条不会自动重打。

网页每 2 秒刷新状态，单次请求 4 秒超时，失败立即清除在线指示灯。服务端将 15 秒未收到心跳的设备视为离线，并将在线、打印机连接和忙碌均置为 false。公共状态附带 `lastSeen/serverTime/staleAfter`。心跳上报中的硬件字段仍是采样数据，不代表每次心跳都重新查询所有设备字段；观测之后设备仍可能断开。

任务状态由服务端保存：排队 queued → 认领 claimed → 发送 printing → 发送完毕 sent → 纸面完成 confirmed。sent 仅表示图像和送纸命令发送流程结束，不能证明纸面完整。用户核对纸面后点击“我已核对纸面，确认打印完成”，服务端记录人工确认 confirmed。网页每 2 秒凭回执查询，包括 sent 之后，直至 confirmed/rejected/unknown。发送进度写入 detail。已认领/发送中任务 300 秒无更新，后续服务端操作会转为 unknown；即使 Mac 离线，网页回执查询也能触发过期检查。服务器停机时不执行计时任务，重新启动查询后处理。排队任务继续保留。网页只在内存保留当前一份回执，刷新页面不会自动恢复跟踪；持有回执仍可调用 API 查询。

在远程打印页展开“用户密钥管理”，输入独立管理员密钥；普通设备密钥不能管理用户。可以从服务器刷新列表、新建不同角色和目标设备的密钥、复制仅本次显示的原始密钥、更改展示名称、吊销、删除已吊销记录。改名只影响后续投稿，历史任务保留原身份。当前管理员不能通过这些接口改名、吊销或删除自身。必须同时更新客户端和服务端代码。

新增 POST API（JSON）：

| 路径 | 权限 | 请求与结果 |
|---|---|---|
| `/v1/admin/keys/{id}/rename` | admin | `{"name":"Alice"}` → `{"updated":true}`，去除首尾空白后 1–80 字符 |
| `/v1/admin/keys/{id}/delete` | admin | `{}` → `{"updated":true}`，必须先吊销，否则 409 |
| `/v1/device/jobs/{id}/confirm` | 目标设备 device | `{}` → `{"state":"confirmed"}`，仅限已发送或已确认任务 |

找不到密钥返回 404；操作当前管理员自身返回 409。确认状态是人工观察，不是设备遥测。

新打印默认参数：BLE 间隔由 30 降为 10 ms，图像帧间隔由 80 降为 0 ms，发送队列等待检查由 30 降为 5 ms。保留设备额度流控、CoreBluetooth 背压、命令确认、纸仓检查、浓度回读和末尾 2 秒等待。导入的旧预设保留原参数，工程界面可恢复 30/80 ms。减少的是固定传输等待，不能提高打印头机械速度；本次软件测试不代表已经测量了实物打印速度或质量。

## 分区进度与 12 位 ID 查询

网页会解析现有 Mac 客户端结果回报中的 `第1/1份 · 分包20/90`，显示
`20 / 90 · 22.2%`。本次不修改 Mac 源码、蓝牙协议，也不需要重新构建客户端。
这是**已发送到打印机的光栅分区数**，不等于独立测得的纸面完成量。客户端约每
2 秒回报一次，较快任务可能来不及采到中间计数。

原收据接口、设备列表/详情、短 ID 查询均增加可为空的字段：

```json
{"progress":{"completed":20,"total":90,"unit":"partitions","basis":"transport","percent":22.2}}
```

一个进度条包含 Queued、Accepted、Printing、Sent、Confirmed 五个等宽步骤，仅在
Printing 段按分区数细化，20/90 表示填充该段的 22.2%，而非整条流程的 22.2%。没有
计数时停在 Printing 段起点并等待回报，不虚构百分比；Sent 不会填满 Confirmed。
这些位置是流程步骤，不是耗时预测。

可见标签页中，排队每 500ms 查询，认领/打印每 250ms 查询，已发送每秒查询以等待
纸面确认；隐藏标签页降为每 4 秒查询。自动轮询等待上一请求返回后再调度，不叠加
请求。Mac 回报仍约每 2 秒一次，更快网页轮询不会凭空增加客户端采样。

计数合并所有份数，尚未收到计数时 progress 为 null。过期或畸形回报不会降低进度或改变总量。`sent` 会完成已知的传输总量，但
仍等待收件人手工确认 `confirmed`；`unknown` 保留最后部分计数，不自动重打。

在网页 **Track** 输入 12 位十六进制 ID，支持大小写及 `ABCD-EF12-3456` 分组。
接口是 `GET /v1/jobs/lookup/{shortID}`。仅凭 ID 返回状态、创建/更新时间、传输进度
以及 `detailsAvailable:false`；不公开内部完整 ID、正文、IP/浏览器信息或设备自由
填写的诊断详情。过期或删除任务返回 404。

查询完整详情需要原始 `X-Job-Receipt`，或者管理员、本任务所属设备、提交该任务的
同一投稿密钥。其他有效密钥只能看到公开字段；错误收据返回 404，无效/已吊销密钥
返回 401。历史任务未记录投稿密钥关联，不能仅凭相同展示名推断所有权，需使用
收据或管理员/所属设备密钥。凭据只放请求头，不放 URL。

网页会在当前标签页的 sessionStorage 保留最近 20 个原始收据，便于按 ID 找回自己
的详情；不存储访问密钥、正文或访客元数据，正文使用纯文本渲染。输入投稿密钥也
可以查看由该密钥提交的任务。关闭标签页后，不能保证找回匿名任务的私有详情。

公开状态接口新增 `capabilities:{partitionProgress:"transport",shortIDLookup:true}`。
服务启动自动增加可空的 `progress`、`submit_key` 列，保留现有任务、密钥和收据，
并回填仍保存分包状态的旧记录。已完成旧任务若计数被最终详情覆盖，就不推测其总量。
升级前一致性备份 SQLite；回滚应同时恢复代码和对应数据库快照。

### 密钥轮换与恢复

禁止误吊销当前请求使用的管理员凭据、目标设备最后一个有效 admin/device，以及
在线设备正在回报的 device key。先创建替代密钥、在 Mac 更新并确认重连，再吊销旧
密钥。吊销立即失效，删除记录不会自动生成替代凭据。

锁定后可在服务器以服务账户通过现有配置执行本地 `key-add`，生成新的 admin/device，
无需删除数据库。`key-revoke KEY_ID --allow-lockout` 是明确的本地紧急覆盖，可使设备
断线；API 没有绕过保护的参数。保护原始密钥导出，恢复历史备份也会恢复当时的密钥状态。

本次服务端和浏览器测试不表示重新构建过 Mac App 或验证过实物出纸。

数据位置、历史收据和备份保留见[存储说明](RELAY-DATA.zh-CN.md)。公开状态的
`policy.retention_days` 返回实际配置值，任务默认从最后更新起保留 7 天。

## 富媒体与状态修复

详见[绘图与内容 API](RICH-MEDIA.zh-CN.md)：版本化点阵、能力协商、行数配额、隐私与限制。POST /v1/jobs 支持 160 KiB，其他接口仍为 64 KiB。unknown 任务附带 lastStage。
