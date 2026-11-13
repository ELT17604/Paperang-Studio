# 参与开发

使用 `./Android/build.sh` 构建 AndroidClient，并运行 `tests/android/check.sh`。用 `tests/android/runtime.sh <模拟器序列号>` 执行安卓运行测试。[AndroidClient](../Android/README.md)

发行遵循[版本规则](VERSIONING.zh-CN.md)。相关变更同步更新 [VERSIONS.json](../VERSIONS.json) 和[功能实现矩阵](FEATURE-MATRIX.zh-CN.md)，运行 `python scripts/check-versions.py`。同基线共享契约必须互通；发行版功能裁剪需明确标注，不声称未验证的一致性。

[English](../CONTRIBUTING.md) · [中文文档目录](INDEX.zh-CN.md)

先阅读doc/PROTOCOL.md、doc/ARCHITECTURE.md和doc/ACQUISITION.md。协议更改请附固定源码链接或脱敏抓包，标明设备/固件、CRC、ACK与字段、实际纸面反馈；不得把模拟值写成实测。保持576点/完整行分包和单设备共享锁。

新增耗纸入口接入侧栏唯一全局开关；默认不连接、不打印，不自动重发失败任务。修改HTTP路由时同步doc/API.md、doc/openapi.json与Clients。涉及UI请实际查看界面，涉及硬件请记录验证边界。

构建/测试输出放外部开发目录；不要提交缓存、令牌、原始个人设备标识、会话日志或下载的其他仓库。贡献与改编遵循LICENSE的CC BY-NC-SA4.0条款，保留ELT Group署名与NOTICE；发布到其他电脑时配置正式签名/公证。
