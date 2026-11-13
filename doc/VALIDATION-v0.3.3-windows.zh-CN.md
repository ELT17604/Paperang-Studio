# WinLite0.3.3 Windows验证记录

[English](VALIDATION-v0.3.3-windows.md) · [发行/升级](RELEASE-v0.3.3-winlite.zh-CN.md) · [复现测试](../tests/windows/README.md)

2026-11-10，Asia/Shanghai。`dev/p2-usb-protocol-research`，基于v0.3.2 / `46d55b7d6782d02ba0ff296e0af3c79a7f19b674`。本地候选构建，未发布GitHub、部署生产或覆盖用户安装。

仅Windows版本升0.3.3-winlite；其他端/共同合同保持0.3.2、API v2、schema1。原AppId、安装/数据目录、私钥、DPAPI、待回报结果、模板/渲染与自动打印偏好保留。新连接偏好缺失时默认auto，手动bluetooth选择可持久保存；不关闭Windows系统蓝牙，原BLE GATT实现未改。

| 检查 | 实际结果 |
| --- | --- |
| 原生Release构建 | .NET SDK10.0.403，Windows x64通过，最终无警告/错误 |
| 原生回归 | **73项通过**，覆盖既有BLE/模板/DPAPI/任务不重打及USB帧/CRC/倒计数/纸仓/浓度、连接选择/回退/取消/未知打印隔离 |
| 隔离真实Relay联调 | **12项通过**：原生RSA所有者证明、会话续期、C权限/M拒绝、真实渲染/engine的文字与raster1，使用模拟打印机和临时数据库 |
| Python诊断 | **20项通过**，黄金帧、损坏头恢复、ACK/数据、纸状态三值、脱敏/边界 |
| 版本/共同合同 | check-versions.py、test_contract_inventory.py通过，仅放行有文档的Windows0.3.3例外 |
| 实体USB连接 | P2、电量100%、合盖有纸，断开重连通过 |
| 实体USB→BLE→USB | 修复CRC状态后通过；只连接记住的广播P2，BLE握手/纸仓通过，释放BLE并恢复USB |
| 实际设置页 | 使用隔离偏好、真实传输、不轮询生产中转；USB连接后蓝牙选择/扫描禁用，手动BLE释放USB并开放扫描；扫描后BLE显示100%/合盖有纸，最终USB界面再次禁用蓝牙控件 |
| 原生USB实物打印 | 576×56、四包3/2/1/0、浓度100/送纸150；用户明确回复“完整、顺序正确、浓度正常” |
| 发布物构建 | 自带运行时portable、依赖运行时lite、Inno Setup6.7.3安装器及各自SHA256均生成 |

一次重复联调因隔离UI仍加载测试DLL发生文件锁；窗口退出后重新构建/运行，12项全部通过。该次失败如实保留，未当作产品传输故障。

跨传输新发现：BLE握手注册key0后，USB也只接受seed0；默认seed型号GET无数据，seed0型号GET有CRC正确响应。原生/Python工具现只用型号GET识别两种已知seed，不执行重置/CRC注册，不重发点阵。修复后只读硬件切换闭环通过；不为只读测试虚构新纸面确认。

发布物位于仓库旁 `PaperangStudio-Windows-Development/Release/`，名称为 `PaperangStudio-0.3.3-winlite-win-x64-setup.exe`、`V0.3.3-winlite-win-x64-portable.zip`、`V0.3.3-winlite-win-x64-lite.zip`及SHA256；程序、图标、README、LICENSE、NOTICE齐全，不含用户配置/私钥/原始设备标识/捕获。

本轮未对用户正式安装执行覆盖/卸载、未重启/真实登录、未测ARM64/干净虚拟机、未做生产投稿到纸面的新闭环；0.3.2历史安装验收不改标为0.3.3。包沿用未签名状态。

设备打印中、机械完成、充电仍未知。四包纸面已确认，但固件最大长度/缓存、长图吞吐、打印中拔线和其他Windows/打印机仍未实测；不确定写入不跨传输重打。[USB完整证据](USB-PROTOCOL.zh-CN.md) · [升级与回滚](RELEASE-v0.3.3-winlite.zh-CN.md)。
