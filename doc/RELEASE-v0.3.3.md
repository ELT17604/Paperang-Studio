# Paperang Studio v0.3.3

本次为 AndroidClient、WinClient、MacClient 和 Relay 四条主线的统一发布，标签为 **v0.3.3**。文件名统一使用产品发布版本；组件内部版本保留，便于识别实际构建。WinLite 暂停维护，不包含在本次发布中。

## 下载与实际版本

| 文件 | 平台与要求 | 内部版本 |
|---|---|---|
| `PaperangStudio-0.3.3-androidclient-android.apk` | Android 13+；本地 debug 签名 APK | `0.3.3-androidclient-opt2`，versionCode 12 |
| `PaperangStudio-0.3.3-winclient-windows-x64-setup.exe` | Windows 10 2004+ / Windows 11，x64 | `0.3.3-winclient-dev5` |
| `PaperangStudio-0.3.3-macclient-macos-arm64.zip` | macOS 14+，Apple Silicon；本地 ad-hoc 签名 | `0.3.3-macclient-opt2`，build 3303 |
| `PaperangStudio-0.3.3-relay-linux-source.tar.gz` | Linux，Python 3.10+、systemd | `0.3.2-linuxrelayserver-opt1`，当前主线源码 |
| `SHA256SUMS` | 所有发布文件的 SHA-256 | — |

Relay 本次发布的是当前主线的可部署源码快照，内部版本仍为 0.3.2，不代表新增 Relay 0.3.3 功能。Windows 安装包采用用户提供的 dev5 构建，仍带开发版本后缀。

## 更新内容

- **MacClient**：系统字体选择、可配置二维码、自由排版及局部文字格式，RAW/raster1 导入；默认零边距、576 点满宽及批后送纸 160 行；改善 BLE 写入节奏并忽略零长度通告，省去无效帧间等待。
- **AndroidClient**：文字、图片、二维码、多图层排版、全屏手绘、本地素材和可恢复打印历史；按键触感开关；与 Mac 对齐零边距、576 点满宽、批后送纸 160 行，集中管理 BLE 传输常量。
- **WinClient**：统一 Windows 桌面工作台、USB 优先与 BLE 回退、排版和二维码、终端、Relay 队列、管理及本机 API；提供独立 x64 安装包，替代暂停维护的 WinLite 主线。
- **Relay**：沿用 Relay API v2、浏览器身份与分级权限、设备任务领取、RSA 所有者验证、终端管理，以及保留数据的升级与程序回滚工具。

## 安装与升级

Android 下载 APK 后安装，覆盖安装须使用相同签名；请保留已有历史。Mac 解压后放入 Applications；包未进行 Apple 公证。Windows 运行安装程序，安装包内部版本为 dev5。

Relay 解压后阅读包内 `Server/README.md`。首次安装使用 `sudo bash Server/install.sh`；已有部署先备份并按照 [升级与回滚指南](UPGRADE-v0.3.2.md) 操作。包根目录含 `manifest.json`，供 `Server/deploy.py` 校验。发布不会自动修改任何在线服务器。

兼容基线保持 **0.3.x**：Relay API v2、本机 API v2、内容及模板 schema 1；P2 576 点协议保持兼容。旧 HTTP API v1 已退役。ChatGPT MCP、RelaySkill、LocalBLESkill 的支持仍暂停。

## 验证范围

- Mac：arm64 release 构建、签名、47 项实际 HTTP 请求、原生中继及隔离 Keychain 集成检查通过。
- Android：APK 编译、v3 签名、ZIP 对齐，以及协议帧、CRC、打印设置、布局与 BLE 节奏回归通过；未连接设备或模拟器，未验证安装运行。
- Relay：56 项服务器回归及 5 项 v2 集成测试通过。测试运行于 macOS；Linux 生产部署未在本次执行。macOS 临时目录使用规范化路径，避免 `/var` 与 `/private/var` 的测试断言差异。
- Windows：确认现有安装包内嵌版本为 dev5并计算校验和；本次未在 Windows 上重新构建或执行安装测试。
- 本次未进行实体打印机的蓝牙、USB 或纸面打印验证。

发布后可从 [v0.3.3 下载页](https://github.com/ELT17604/Paperang-Studio/releases/tag/v0.3.3) 获取文件。下载后请对照 `SHA256SUMS` 校验。
