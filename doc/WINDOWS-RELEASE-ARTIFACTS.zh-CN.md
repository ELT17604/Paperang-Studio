# Windows 发布物与本地程序

版本：v0.3.1-winlite-fix3。构建输出在仓库旁 `PaperangStudio-Windows-Development/Release/`，不包含用户配置或私钥。

| 文件 | 可发布到 GitHub Release | 用途 |
| --- | --- | --- |
| `PaperangStudio-0.3.1-winlite-fix3-win-x64-setup.exe` + `.sha256` | 是，首选 | 标准安装包，自带 .NET；系统应用列表、开始菜单、卸载器 |
| `V0.3.1-winlite-fix3-win-x64-portable.zip` + `.sha256` | 是，可选 | 自带运行环境的免安装版本 |
| `V0.3.1-winlite-fix3-win-x64-lite.zip` + `.sha256` | 是，可选 | 更小的免安装版本，需要 .NET 10 Desktop Runtime |
| ZIP 内 `PaperangStudio.exe` | 是，但建议发布含许可证的完整 ZIP | 客户端本体，不是安装器 |
| `%LOCALAPPDATA%/Programs/Paperang Studio WinLite/` | 不作为发行物 | 安装后的本机正式运行目录，含机器安装状态及卸载程序 |
| `%LOCALAPPDATA%/ELT/PaperangStudioWinLite/` | 否 | 本机加密配置、模板、待回报任务、所有者私钥等 |
| `artifacts/`、配置导出、owner 文件、服务器备份 | 否 | 调试或私人数据，不能批量上传 |

所有发布物使用同一份干净构建载荷，不复制 AppData。安装器按当前用户安装，升级时保留数据。卸载默认保留数据，用户可明确选择删除本程序数据目录，外部路径私钥不会删除；服务器上的授权须由管理者另行吊销。源码目录与开发工具不属于安装内容。

更新检查按照 `vX.Y.Z-winlite[-fixN]` 正式 Release 标签比较，不根据源代码提交宣称已有可下载的新版本。GitHub 未创建 Release 或没有安装包时，必须先由发布者上传上述包。当前构建未配置代码签名，Windows 可能显示未知发布者。
