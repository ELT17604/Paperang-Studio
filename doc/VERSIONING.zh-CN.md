# 版本编号与兼容规则

[English](VERSIONING.md) · [功能矩阵](FEATURE-MATRIX.zh-CN.md) · [版本登记](../VERSIONS.json)

## 产品发行系列

Paperang Studio 使用 `MAJOR.MINOR.PATCH` 三段编号。项目在 1.0 前使用第二段标识主要产品代际。以 `0.3.2` 为例：

| 字段 | 值 | 项目含义 |
|---|---:|---|
| MAJOR | 0 | 产品处于 1.0 前的开发阶段。该阶段可以发布可用的安装包。 |
| MINOR | 3 | 第三代产品发行系列，其兼容范围记作 `0.3.x`。 |
| PATCH | 2 | 第三代系列内的具体修订。各发行版可独立递增。 |

同一发行系列内，共享接口保持兼容。跨代升级也可以沿用原协议。协议或数据格式发生不兼容变更时，显式更新对应的 API 或 schema 版本，并提供迁移路径。

## 当前版本域

| 版本域 | 当前值 | 适用范围 |
|---|---|---|
| 产品兼容基线 | `0.3.x` | 实现各自所用共享契约的发行版 |
| 已发布的 GitHub 产品版本 | `v0.3.2` | 发布包和发行说明 |
| Relay HTTP API | `v2` | LinuxRelayServer、MacClient 与 WinLite 的中转接口 |
| Mac Local HTTP API | `v2` | MacClient 独立的本机 HTTP 接口 |
| 内容、模板、保存配置 | schema 1 | 各格式独立版本，按平台实际支持情况使用 |
| 打印机传输 | Legacy P2 576 点 BLE；WinLite 另支持 P2 USB | 设备传输与 HTTP API 独立 |

## 单端发行与构建

发行标识使用 `vMAJOR.MINOR.PATCH-distribution[-note]`，如 `v0.3.3-androidclient`。运行时可省略前导 `v`。各端可以使用不同的修订和构建号，例如 MacClient 的 bundle build `3302`、AndroidClient 的 `versionCode 10` 和 WinLite 的程序集版本 `0.3.3.0`。这些编号用于相应平台的安装、更新和追踪。组件之间使用明确的 API、schema 和能力字段协商。

`fixN`、`optN` 与 `alphaN`、`betaN`、`rcN` 可标记单端修订或候选构建。保留已发布的标签及构件名称。更新某端时，同步该端的版本源、登记表、变更记录和功能矩阵，运行 `python3 scripts/check-versions.py` 及相应平台的本地测试。平台功能见[功能矩阵](FEATURE-MATRIX.zh-CN.md)。

[v0.3.2 发行记录](RELEASE-v0.3.2.md) · [API v2 契约](API-v2-032.md)
