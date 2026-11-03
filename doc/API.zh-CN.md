# 本机 HTTP API v1

[English](API.md) · [中文文档目录](INDEX.zh-CN.md)

API由App内置Network.framework服务提供。基础地址 `http://127.0.0.1:8176`，默认关闭、仅回环监听。应用工作台、TTY、API共用连接与打印互斥锁；API不会另外抢占打印机。

## 启动与身份验证

App → 本地API → 设置端口（1024–65535）、令牌和网页Origin → 启动。令牌32–256个非空白可打印ASCII字符，默认随机生成、不写入仓库或普通日志；重启App后重新生成。运行时不能修改端口/令牌/Origin，需停止后修改。

每个实际请求必须携带：

```
Authorization: Bearer <本机令牌>
Content-Type: application/json
```

GET不要求Content-Type，POST必须是JSON对象，即使没有参数也发送 `{}`。Host只允许 `127.0.0.1:端口` 或 `localhost:端口`。网页额外需要Origin精确匹配；默认允许`http://localhost:3000`及`http://127.0.0.1:3000`，不支持通配符、null来源。OPTIONS预检只在Origin白名单内免令牌；返回允许Authorization/Content-Type及私有网络请求头。浏览器可能另外限制HTTPS→本机HTTP，必须遵循浏览器的本地网络访问政策。

不要把令牌写入公开网页代码、URL或远程服务。此API供同一台电脑的软件使用，不是公网代理。未提供shell、TTY输入、任意文件读取或原始未知协议写入接口。

命令行可显式启用（正常GUI会话）：

```sh
'/path/Paperang Studio.app/Contents/MacOS/PaperangLab' \
  --api --api-port 8176 --api-token-file /absolute/private-token-file
```

令牌文件UTF-8、尾部换行会去除，建议权限0600。加 `--api-offline` 可无GUI运行纯离线渲染服务，不初始化蓝牙；扫描/连接不可用，耗纸仍禁用。退出该命令即停止服务。端口冲突时启动失败，不改系统配置。

## 接口清单

| 方法/路径 | 请求 | 返回/语义 |
|---|---|---|
| GET `/v1/status` | 无 | 当前连接、busy、printer、state、lastError、progress、jobStatus、charging、readings、printingEnabled、apiVersion |
| GET `/v1/devices` | `?all=true`可选 | `{scanning,devices:[{id,name,rssi,paperangCandidate}]}`；默认仅系列候选，列表来自最近扫描 |
| POST `/v1/scan` | `{}` | 202 `{accepted,scanning}`；扫描约10秒，不自动连接；随后GET devices |
| POST `/v1/connect` | `{"id":"扫描返回UUID"}` | 202请求已提交；轮询status直到connected=true。UUID由本机CoreBluetooth分配，不是MAC |
| POST `/v1/disconnect` | `{}` | `{connected:false}`；打印中409，先取消任务 |
| POST `/v1/query` | `{"command":12}` | `{command,payloadHex,hasData,status}`；仅白名单GET，十进制整数命令；只有ACK时hasData=false、payloadHex=null |
| GET `/v1/protocol` | 无 | 型号、576点/72字节、300DPI、150送纸与commands目录（名称、证据、说明、响应）。研究目录不代表可调用 |
| GET `/v1/settings` | 无 | `{defaults:PrintSettings,note}`；每次渲染/打印从确定的默认值合并settings，不继承当前GUI编辑值 |
| POST `/v1/render` | RenderInput | 离线渲染；`{width,height,bytes,previewPngBase64}`，includeRaw=true另含rawBase64；不耗纸、不要求蓝牙 |
| POST `/v1/jobs` | RenderInput + allowPaper/title | 202 `{id,state:"accepted",note}`；启动打印，轮询任务结果；忙时409，不排队自动重打 |
| GET `/v1/jobs` | 无 | `{jobs:[Job]}`；最新在前，最近30个会话任务，App重启清空 |
| GET `/v1/jobs/{id}` | 无 | 单个Job |
| POST `/v1/jobs/{id}/cancel` | `{}` | 仅当前发送中任务；停止并断开，设备已缓存数据无法撤回；返回cancelled并非纸面撤销 |
| POST `/v1/feed` | `{"lines":150,"allowPaper":true}` | lines整数0–2000；`{acknowledged:true,lines,physicalCompletion:"unknown"}`，等待媒体检查和ACK |
| GET `/v1/logs` | `?limit=100`，1–500 | `{order:"newest_first",logs:[{timestamp,type,text}]}`；可能包含本机设备信息 |
| GET `/v1/openapi` | 无 | 完整OpenAPI3.0.3；也见[离线文件](openapi.json) |

### 状态与任务

`readings`元素：`{command,name,value,rawHex,sampledAt}`。这些是最近采样，不保证实时；需要新值时POST query。充电字段固定`"unknown"`，不会根据电量上升推断实时正在充电。

Job：`{id,title,detail,state,createdAt}`，时间ISO8601。state目前保留GUI中文值：`发送中`、`已发送，待核对纸面`、`未完成`、`已停止`。`progress`是全局活动任务进度。202仅接受；“已发送”也只是数据及ACK流程结束，调用方应让用户确认纸面。没有固件机械完成事件或持久任务队列。失败后不自动重试，避免重复耗纸。

### RenderInput

必须且只能提供 `text`（字符串）或 `imageBase64`（PNG/JPEG等ImageIO可解码图片的原始文件Base64，不带data:前缀）。可选 `settings` 部分对象、`includeRaw`真正布尔值；jobs另接受 `title`（截取120字符）、`allowPaper:true`。

图片不接收路径或远程URL；请求正文上限2MiB，因此Base64解码后的实用图片上限约1.5MiB，另有限制源图4000万像素及解码缩略图4096。文本上限10000字符、输出高度1400像素；不是固件上限。图像PNG/raw均对应同一份576列最终位图。

### 耗纸授权

POST jobs/feed需要同时满足：App侧栏“全局允许耗纸”、请求JSON中的真正布尔值`allowPaper:true`。数值1或字符串true无效。全局开关默认关闭；命令行启用API不会启用打印。打印前读纸/盖、设置浓度并回读；GUI/API/TTY共享busy锁。

## 全部settings字段

几何单位为最终输出点/像素，传输间隔为毫秒，等待为秒，feed为设备行。下表是v1可覆盖字段；未列字段报400。完整机器约束见OpenAPI Settings。

| 字段 | 默认 | 范围/说明 |
|---|---|---|
| version | 1 | 仅1 |
| fontName / fontSize / bold | PingFang SC / 30 / false | 系统字体名称；8–96，主机文字渲染 |
| lineSpacing / alignment | 4 / 居中 | 0–40；居左/居中/居右 |
| marginX / marginY | 12 / 12 | 0–120 / 0–200 |
| contentWidth | 552 | 48–576，且contentWidth+2×marginX≤576 |
| imageFit | 适配宽度 | 适配宽度/保持像素/裁切填满 |
| cropHeight / rotation | 400 / 0 | 24–1400；0/90/180/270 |
| conversion | 阈值 | 阈值/Floyd–Steinberg/有序抖动 |
| threshold / brightness | 128 / 0 | 0–255 / -100–100 |
| contrast / gamma | 1 / 1 | 各0.2–3 |
| invert | false | 黑白反相 |
| density / copies | 100 / 1 | 整数0–100 / 1–20 |
| feedBefore / feedAfter | 0 / 150 | 整数0–2000；每批前后送纸 |
| feedBetweenCopies | false | 每份之间额外送纸 |
| packetBytes | 1008 | 72–1008且为72整数倍 |
| bleChunk | 180 | 整数20–180，另受CoreBluetooth最大长度限制 |
| writePauseMS / framePauseMS | 30 / 80 | 整数5–200 / 0–1000 |
| timeoutSeconds / finishWaitSeconds | 6 / 2 | 整数3–30 / 1–15 |

## 错误及连接约束

错误JSON：`{"error":{"code":"paper_disabled","message":"..."}}`。400格式/渲染/参数/协议操作失败；403令牌、Host、Origin或耗纸开关；404路由/任务不存在；409设备未就绪、busy或离线；415 POST类型错误；429请求过快；503服务未就绪。JSON的message供用户阅读，调用方主要依据HTTP状态及code处理；不应无限重试。

HTTP/1.1，仅GET/POST/OPTIONS；最多8连接、每连接12秒、每秒20个已认证请求；头部16KiB、正文2MiB。不支持chunked、流水线或持久连接；重复头部拒绝。响应no-store。合理轮询0.5–1秒即可。

## 示例

先在GUI复制令牌，并存入仅本机使用的环境变量`PAPERANG_TOKEN`。

```sh
curl --noproxy '*' -H "Authorization: Bearer $PAPERANG_TOKEN" \
  http://127.0.0.1:8176/v1/status
curl --noproxy '*' -H "Authorization: Bearer $PAPERANG_TOKEN" \
  -H 'Content-Type: application/json' -d '{"text":"你好 P2","settings":{"fontSize":30},"includeRaw":true}' \
  http://127.0.0.1:8176/v1/render
```

Python（将Clients加入模块路径）：

```python
import os
from paperang_client import PaperangClient
p = PaperangClient(os.environ['PAPERANG_TOKEN'])
preview = p.render('Hello Paperang P2!', {'density': 100, 'feedAfter': 150})
# 在App中完成连接和侧栏全局耗纸授权后，显式提交：
job = p.print_text('Hello Paperang P2!', allow_paper=True)
print(p.wait(job['id']))
```

网页/Node（网页Origin需在App白名单）：

```js
import {PaperangClient} from './Clients/paperang-client.js';
const p = new PaperangClient(tokenEnteredLocallyByUser);
const preview = await p.render('Hello P2', {fontSize: 24});
imageElement.src = `data:image/png;base64,${preview.previewPngBase64}`;
// 由用户点击打印后执行，另需App全局授权：
const job = await p.printText('Hello P2', {allowPaper: true});
console.log(await p.job(job.id));
```

客户端是薄封装；其他端点可用其request方法。Python绕过系统HTTP代理访问回环，JavaScript调用浏览器/Node的fetch。API版本固定/v1；新增能力以兼容字段扩展，破坏性变更另开版本。
