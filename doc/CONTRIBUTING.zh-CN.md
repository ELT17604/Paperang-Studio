# 参与开发

使用 `./Android/build.sh` 构建 AndroidClient，并运行 `tests/android/check.sh`。用 `tests/android/runtime.sh <模拟器序列号>` 执行安卓运行测试。[AndroidClient](../Android/README.md)

发行遵循[版本规则](VERSIONING.zh-CN.md)。相关变更同步更新 [VERSIONS.json](../VERSIONS.json) 和[功能实现矩阵](FEATURE-MATRIX.zh-CN.md)，运行 `python scripts/check-versions.py`。同基线共享契约必须互通；发行版功能裁剪需明确标注，不声称未验证的一致性。

[English](../CONTRIBUTING.md) · [中文文档目录](INDEX.zh-CN.md)

先阅读doc/PROTOCOL.md、doc/ARCHITECTURE.md和doc/ACQUISITION.md。协议更改请附固定源码链接或脱敏抓包，标明设备/固件、CRC、ACK与字段、实际纸面反馈；不得把模拟值写成实测。保持576点/完整行分包和单设备共享锁。

新增耗纸入口接入侧栏唯一全局开关，启动时关闭。接收器自动重连遵循已保存的平台偏好，但不能据此授权耗纸或重放失败任务。修改 HTTP 路由时同步 doc/API.md、doc/openapi.json 与 Clients。涉及 UI 请实际查看界面，涉及硬件请记录验证边界。

Windows 当前目标为 WinClient。桌面夹具放在 `tests/windows/WinClientChecks`，只用于验收构建，生产包排除测试代码。推送前执行 `scripts/check-source.py`、`scripts/check-docs.py`、`scripts/check-versions.py`、`tests/test_contract_inventory.py` 和相关平台检查。Wiki 读取已提交源码，因此文档提交后、推送前再执行 Wiki 构建与检查。

构建/测试输出放外部开发目录；不要提交缓存、令牌、原始个人设备标识、会话日志或下载的其他仓库。贡献与改编遵循LICENSE的CC BY-NC-SA4.0条款，保留ELT Group署名与NOTICE；发布到其他电脑时配置正式签名/公证。
