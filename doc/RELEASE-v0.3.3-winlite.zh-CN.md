# WinLite v0.3.3 — USB 优先连接

[English](RELEASE-v0.3.3-winlite.md) · [Windows指南](WINDOWS.zh-CN.md) · [版本规则](VERSIONING.zh-CN.md) · [验证记录](VALIDATION-v0.3.3-windows.md)

WinLite 0.3.3 的运行时为 `0.3.3-winlite`，程序集和文件版本为 `0.3.3.0`。该版本沿用 `0.3.x` 兼容基线、Relay HTTP API v2 和 schema 1。单独升级 WinLite 可继续使用 LinuxRelayServer 0.3.2 及已有所有者凭证。

## 连接方式

- 默认“自动连接（USB优先，蓝牙备用）”：每次连接/重连先检查唯一的USB P2，通过Windows原有usbprint.sys通信；USB枚举/握手失败才尝试记住的蓝牙地址。不会自动选择另一台附近蓝牙打印机。
- USB成功后释放本客户端BLE会话，禁用蓝牙扫描和选择；不修改Windows系统蓝牙开关，不影响其他蓝牙设备。
- “断开USB，使用蓝牙”会保存 `printerTransport: "bluetooth"`、释放USB并尝试已保存BLE设备；即使蓝牙连接失败，也开放扫描。直到点击“恢复USB优先并连接”（`auto`）才恢复USB探测。
- 正在打印时阻止切换。USB打印失败不会改走蓝牙重打。健康BLE会话不会因插线被中途抢占；空闲时点恢复/重连重新选择即可。
- 自动打印授权、中转轮询、打印机30秒重试、模板/渲染、浓度/送纸、所有者身份、加密存储与持久结果机制保留。正常托盘仍沿用原中转生命周期连接打印机；独立硬件测试可以不连接中转。

原生USB使用在场SetupAPI枚举、可取消异步USBPRINT读写、只读型号识别默认CRC seed35769521或BLE保留的0、完整行1008字节和剩余包数倒计数。BLE FF03/key0/index递增路径保留。打印前检查型号、最新纸仓及浓度回读，正常状态显示电量/纸仓。全部17个诊断GET、实测与未知项见[完整USB协议](USB-PROTOCOL.zh-CN.md)。

## 升级、回滚与更新同步

1. 完成当前打印及待回报任务，正常退出旧托盘。安装 `PaperangStudio-0.3.3-winlite-win-x64-setup.exe`，或解压完整portable/lite ZIP到固定位置。AppId、安装/数据目录、启动项和私钥路径保留。
2. 旧schema1配置无需重置/迁移；缺失的新字段默认 `auto`。原BLE地址、凭证、模板、授权、浓度/送纸仍保留。USB不需要先保存蓝牙地址或安装新驱动。
3. 下次连接默认USB；需要BLE时选“仅蓝牙”保存并连接，再按需扫描。不要为切换打印机传输重新生成所有者私钥。
4. 回滚时先完成/回报待处理任务，退出后装回0.3.2 WinLite并保留数据；旧JSON读取会忽略新增偏好并继续BLE，不具备USB。独立保存加密数据备份，不删除unknown记录强行回滚。

发布应使用独立 `v0.3.3-winlite` 标签及同版本安装包/ZIP/SHA256，保留历史 `v0.3.2`，不把0.3.3包挂到旧版本、不声称全平台已升级0.3.3。现有手动更新检查支持WinLite标签，0.3.3高于0.3.2，仅打开实际发布的GitHub页面；源码有新版不等于已有下载。


## 范围

本轮只扩充WinLite打印机连接，不新增完整WinClient或HTTP API。设备打印中、机械完成、充电状态仍未知；sent只表示主机传输完成，纸面另由用户确认。以[实际验证记录](VALIDATION-v0.3.3-windows.md)为准；未改变macOS/Linux运行时、服务器行为、schema和既有BLE协议。
