# RelaySkill · v0.3.0-relayskill


> opt1: Regression sources are archived outside the shipping repository. See [validation and reproduction](OPTIMIZATION-opt1.zh-CN.md). Historical test paths below refer to Git revision `4393ae0`.
> **v0.3.0:** [鉴权合同](AUTH-v2.zh-CN.md) · [升级步骤](UPGRADE-v0.3.0.zh-CN.md) · [Agent 接手](HANDOFF-v0.3.0.zh-CN.md)。WinLite 设置含 C 图形化管理；历史正文不再向网页/普通执行凭证开放。

[English](RELAY-SKILL.md) · [技能入口](../Skills/paperang-relay-print/SKILL.md) · [版本规则](VERSIONING.zh-CN.md) · [功能矩阵](FEATURE-MATRIX.zh-CN.md)

这是与 MacClient、LinuxRelayServer、WinLite 平行维护的独立**发送端技能**。通过同一中转提交文字/点阵，仍需在线的接收客户端连接蓝牙打印机；不另建中转后台或打印驱动。

## 安装与使用

完整复制 `Skills/paperang-relay-print/` 到智能体技能目录，例如 `~/.codex/skills/paperang-relay-print/`，保留 SKILL.md、agents、references、scripts 和 VERSION。技能自包含，辅助程序无需依赖仓库其他源码即可运行。

Python 3.10+；状态、回执和原生文字提交只需标准库，文字/图片渲染额外需要 Pillow。依赖装在技能目录外的虚拟环境中；Windows 建议 `%LOCALAPPDATA%/PaperangRelaySkill/python/`，不打包运行时或凭证。

调用 `$paperang-relay-print` 并说明内容/参数，例如“打印这张照片，旋转 90 度，使用照片抖动，最高 800 行”。明确打印请求授权对应内容与份数；仅安装技能或制作预览不会打印。

## 能力与边界

原生文字由接收模板排版；预渲染文字支持中文字体、字号、对齐、边距和行距。图片支持 EXIF 方向、旋转、适配/裁切、阈值/Floyd–Steinberg/Bayer、亮度/对比度/Gamma/反相及可选说明。生成固定 576 点宽、受限高度的 raster1，可另存 PNG 预览。

提交前检查服务器能力和配额；POST 前独占保存尝试记录，份数必须明确；通过回执跟踪状态，网络结果不明时不自动重发。sent 仅表示数据发送，不冒充纸面确认。

**浓度、送纸量、当前接收模板仍由本地客户端控制**，不是中转任务参数。接收模板可能缩放图片并添加身份/时间。精确终稿需使用合适的本地正文模板，不能假装未知字段能远程覆盖设置；合成总高仍限 1400 行。[参数参考](../Skills/paperang-relay-print/references/parameters.md)列出可用开关。

## 凭证与兼容

服务器地址来自参数、`PAPERANG_RELAY_URL` 或配置；Windows 可以只读取当前用户加密 WinLite 配置中的地址，绝不拿设备密钥投稿。使用环境变量/私有文件提供 A/B 随机密钥；不借用浏览器匿名 Cookie。配置保存路径而非密钥，尝试记录保存编号和受理/查询权限标志，不再是秘密访问能力。

使用 0.3.0 基线、HTTP v2，raster1 schema 1 不变。独立版本由 [VERSIONS.json](../VERSIONS.json)、VERSION 与 `--version` 对齐；全端源码版本协调更新；部署按各平台验证后执行。

## 验证与打包

```sh
python -m pip install Pillow
python scripts/check-versions.py
python Skills/paperang-relay-print/scripts/relay_print.py --version
```

12 项检查涵盖中文排版、位/行顺序、透明合成、旋转/抖动、错误摘要/未知参数拒绝、源文件保护、配额/离线、回执、明确份数、权限隔离及防重复提交。原中转 HTTP/SQLite 联调用模拟打印机验证文字/图片/说明、认领和最终回执；另验证真实服务只读状态，测试套件不会连接实物打印机。

可打包为 `PaperangStudio-v0.3.0-relayskill.zip`，排除缓存、运行时、凭证和生成任务，并保留仓库 CC BY-NC-SA 4.0 及 ELT Group 署名。

当前版本更正：RelaySkill 使用 API v2，必须提供 A/B 随机密钥，不借用网页匿名 Cookie；A 只受理，B 才可用同身份密钥查询状态。旧 receipt 不再是能力凭证。
