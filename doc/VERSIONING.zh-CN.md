# 版本命名与兼容规则

[English](VERSIONING.md) · [功能实现矩阵](FEATURE-MATRIX.zh-CN.md) · [版本登记表](../VERSIONS.json)

统一格式：**`v主版本.次版本.修订号-distribution[-note]`**，例如 **`v0.2.3-winlite-fix1`**。标签、发布标题、文档使用 `v`；运行时字符串及 VERSION 可省略 `v`。发行版标识固定为小写：`macclient`、`linuxrelayserver`、`winlite`、`relayskill`、`winclient`、`linuxclient`，新版本不再使用 `win_lite` 或大小写变体。

前面的 **0.2.3 是共同功能与兼容基线**：同一代、共享接口/格式兼容、已实现共同功能语义一致。服务端不需要客户端界面，托盘精简版也不自动等于完整客户端；定位差异必须在矩阵逐项说明。完整客户端缺少必需功能时，不能声称已达到同基线完整功能一致。

## 基线与发行版修订

- **主版本 MAJOR**：共享契约出现破坏性变化或进入不兼容的新一代。
- **次版本 MINOR**：跨端协调的新大功能基线；各端满足自身必需功能及互通检查后才能进入。
- **修订号 PATCH**：需要各端协同的共同基线纠正，即使向后兼容也统一更新。
- **`fixN`**：单一发行版的兼容修复或局部改进，fix1、fix2 依次递增。每个“基线 + 发行版”独立计数，换基线后重新计数；不允许借 fix 偷改接口或格式造成不兼容。
- 可使用单个 `alphaN`、`betaN`、`rcN` 预发布说明；无 note 表示该发行版首次稳定发布。不能只按字符串字典顺序比较版本。

新标签不可变。已有 `0.2.3-fix1`、`0.2.3-win_lite` 等保留为历史别名，不重写提交/标签。`VERSIONS.json` 同时登记规范名和实际运行时/文件版本。MacClient、LinuxRelayServer 当前保留部署中的旧运行时标识，通过登记表映射规范名；Windows 发布不会擅自重建或部署其他端。尚未实现的发行版 version 为 null，不虚构已发布版本。

## 0.2.3 共同契约

中转 HTTP v1、Bearer 角色隔离、`text,raster1` 协商、raster1 schema 1 和 SHA-256、模板 JSON version 1、传统 P2 的 576 点/72 字节每行点阵，以及 queued → claimed → printing → sent/unknown/rejected、独立人工 confirmed。ACK 或 sent 不等于纸面完成，不确定/中断任务不自动重打。运行时标签不能替代 API/schema/capability 字段。

平台字体替代、自启方式、重连节奏可以不同，不要求跨系统字形逐像素一致。凭证保存、自动耗纸授权等默认值差异必须标明，不能默认为跨端同等实现。

## 每次发布必须同步

1. 对应发行版 VERSION/运行时元信息、`VERSIONS.json`、变更记录和双语说明。兼容单端修复保留共同数字基线。
2. 功能矩阵中变动项的实现状态、例外原因、源码与验证范围；区分代码审阅、模拟测试、设备实测和纸面确认。
3. 客户端/服务器互通、模板往返、任务状态和不重复打印检查。完整客户端满足必需行；WinLite 主动裁剪保留说明。
4. 运行 `python scripts/check-versions.py` 和对应实现测试。验证打包、不提交凭证/原始个人标识/构建产物，保留许可证。
5. 使用唯一标签/产物名，如 `v0.2.3-winlite-fix1`、`PaperangStudio-v0.2.3-winlite-fix1-win-x64-lite.zip`；不覆盖其他发行版或把规划中的客户端当已实现。
