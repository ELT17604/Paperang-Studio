# 暂停维护与历史分支

本页记录有明确暂停或退役决定的组件。截至 v0.3.3，当前发布主线为 MacClient、AndroidClient、WinClient 和 LinuxRelayServer。保留源码、历史版本或安装的 Skill，不代表该组件仍受到当前主线支持。

## 暂停维护的组件

| 组件 | 当前状态 | 保留内容与当前替代路径 |
|---|---|---|
| WinLite | 暂停功能研发 | 保留 WinForms 前端、历史构建方法及版本记录；Windows 新功能转向 WinClient。旧版本为 `0.3.3-winlite-opt1`，不包含在统一 v0.3.3 发布附件中。 |
| ChatGPT MCP / GPT Actions | 支持暂停 | 不能据此认为当前 ChatGPT 可以直接远程打印。历史接口源码不构成可用的在线服务；当前使用客户端或自托管 Relay。 |
| RelaySkill | 支持暂停 | 历史 v0.3.2 发行记录仅供追溯；当前远程打印以 Relay API v2 和原生接收客户端为准。 |
| LocalBLESkill | 支持暂停 | 历史本地直连实验不作为当前支持入口；使用 MacClient 或 AndroidClient 的 BLE 打印。 |

Skill 与集成的暂停状态从 v0.3.3-macclient 阶段起记入版本登记。暂停意味着当前不承诺继续修复、升级或验证；未宣布永久删除，也没有确定恢复日期。

## 退役的接口

HTTP API v1 已退役，旧路由返回 HTTP 410。它与“暂缓开发”不同，现有消费者需要迁移至 v2。Local API 与 Relay API 是各自独立的接口，不应混用凭据或地址。

## 不属于暂停维护的项目

- **本地位图生成 Skill（paperang-local-raster）**仍作为生成 RAW/raster1、手动导入预览的辅助工具；它不是直接打印 Skill。
- **LinuxClient**在版本登记中为 planned，尚不是可安装发行版；不能描述成曾经维护后暂停。
- **macOS 临时 Relay**是开发用途入口，不等于 Linux 生产部署。
- **USB 协议研究、ESP32 模拟器和第三方实现**作为研究与参考资料保留；现有资料没有将它们全部宣布为暂停分支，不能一概标记为停更。Windows USB 支持已进入 WinClient 主线。

## 历史资料与当前入口

[WinLite 历史指南](WINLITE-LEGACY.zh-CN.md) · [版本与兼容性](VERSIONING.zh-CN.md) · [功能矩阵](FEATURE-MATRIX.zh-CN.md) · [v0.3.3 统一发行说明](RELEASE-v0.3.3.md) · [版本登记](../VERSIONS.json)

本页按组件和支持状态记录，不把 Git 分支是否存在、是否合并或长期没有提交，单独当作已被有意搁置的证据。
