# WinClient 实现与验证

[English](WINCLIENT.md) · [用户指南](WINDOWS.zh-CN.md) · [构建入口](../Windows/README.md)

当前版本为 `0.3.3-winclient-dev5`，兼容基线 `0.3.x`。Windows 使用 WinUI 3、Windows App SDK 2.7.0 与 .NET 10。WinLite 暂停功能研发，当前应用不依赖其可执行项目。

## 目录与构建

- `Windows/src/Paperang.WinClient`：桌面页面与生命周期。
- `Windows/src/Paperang.Windows`：BLE/USB、Windows 渲染、用户加密存储、ConPTY。
- `Windows/src/Paperang.Core`：协议、参数、排版、任务认领与本机 HTTP。
- `tests/windows/WinClientChecks`：独立桌面验收代码，只有指定测试构建才编译，生产应用不包含夹具。

运行 `./Windows/scripts/build-client.ps1`。脚本先生成外部验收包并执行桌面检查，再生成生产包，检查 PRI/XBF、旧应用残留与测试代码残留，最后对生产包执行离线启动检查。`build-client-installer.ps1` 还会生成按用户安装包与 SHA-256。运行中的便携目录不可覆盖，应退出客户端或设置独立的 `PAPERANG_WIN_WORK_DIR`。

## 已验证范围

| 项目 | 检查 |
|---|---|
| 桌面 | 原生窗口/导航/布局、中文渲染、预览导出、图层与旧素材恢复 |
| 蓝牙页面 | 注入连续发现流，验证扫描中选择、信号更新不丢选择、真实列表绑定与停止扫描；不访问实际蓝牙硬件 |
| JSON | Mac/Android schema 1 预设与 raster1，字体别名、参数恢复、坏文件不覆盖内容 |
| 图像/排版 | 透明背景、缩放/旋转/裁切、灰度、富文本、标签、对象源恢复 |
| 二维码 | 18 种格式/模块形态/外框组合经独立 ZXing 解码 |
| 协议与任务 | 95 项 Windows 回归，包含 BLE/USB 分包、媒体/浓度检查、DPAPI、互斥、认领、取消和不自动重打 |
| 中转管理 | 假服务验证 C 登录引导、权限/过期、分页和完整正文查询；未操作真实服务器身份 |
| 本机接口 | 真实环回 HTTP，验证令牌、Host/Origin、路由、参数与耗纸开关 |
| 终端 | 真实 cmd ConPTY 输入/输出/退出，缓冲、隐私与重复关闭 |

[逐控件盘点](MAC-WINCLIENT-CONTROLS.zh-CN.md)覆盖 455 条 Mac 源码声明，用于定位迁移范围；不是所有动态控件均通过人工验收的声明。

## 保留限制

HEIC/HEIF 导入未实现；字体面板使用 Windows 对应能力；API 主要操作可用，但部分字段/返回状态与完整 OpenAPI 请求 schema 尚未严格对齐。API/Shell 随工作台关闭。旧服务端分页上限不自动聚合成 100 行。部分 Mac 托盘快捷项、自动发行版比较、PowerShell 交互、ARM64、安装升级/卸载和真实打印仍需专项验收。

已有 WinLite 物理测试记录只适用于其历史测试范围，不能作为本次 WinClient 硬件验收。触屏专用绘图不在 Windows 对齐范围内。源码推送到 main 不等于创建或发布 GitHub Release。
