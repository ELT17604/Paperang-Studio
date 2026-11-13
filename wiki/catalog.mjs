const doc = (name, en, zh) => [name.toLowerCase(), en, zh, `doc/${name}.md`];

// English and Simplified Chinese share topic IDs. A fourth field points to the
// repository source. Pairs use the .zh-CN convention unless explicitly mapped.
export const groups = [
	['start', 'Start here', '开始使用', [
		['first-print', 'Your first print', '第一次打印'],
		doc('FEATURE-MATRIX', 'Feature matrix', '功能矩阵'),
		doc('VERSIONING', 'Versions and compatibility', '版本与兼容性'),
		['project', 'Project README', '项目介绍', 'README.md', 'doc/README.zh-CN.md'],
		['source-index', 'Repository documentation index', '仓库文档目录', 'doc/README.md', 'doc/INDEX.zh-CN.md'],
		['screenshots', 'Screenshots', '界面截图', 'assets/screenshots/README.md'],
	]],
	['clients', 'Client guides', '客户端指南', [
		doc('USER-GUIDE', 'MacClient', 'MacClient 使用指南'),
		['android', 'AndroidClient', 'AndroidClient 使用指南', 'Android/README.md', null, 'zh-CN'],
		doc('WINDOWS', 'WinLite', 'WinLite 使用指南'),
		doc('MACCLIENT-WORKSPACE', 'MacClient workspace', 'MacClient 打印工作台'),
		doc('TERMINAL', 'Paper terminal', '实体纸带终端'),
		doc('TRAY-MODE', 'Menu bar and tray mode', '菜单栏与托盘模式'),
	]],
	['relay', 'Remote printing', '远程打印', [
		doc('REMOTE-PRINTING', 'Remote printing setup', '远程打印与部署'),
		['relay-server', 'LinuxRelayServer', 'LinuxRelayServer 部署', 'Server/README.md'],
		['relay-console', 'Terminal administration', '终端管理', 'Server/ADMIN-CONSOLE.md'],
		doc('LAYOUT-EDITOR', 'Remote queue and layout editor', '远程队列与排版'),
		doc('RICH-MEDIA', 'Remote drawing and rich media', '远程绘图与富媒体'),
		doc('RELAY-DATA', 'Data and retention', '数据与保留策略'),
		['temporary-relay', 'Temporary macOS relay', 'macOS 临时中转', 'Server/macOS-Temporary/README.md', 'doc/MACOS-TEMPORARY.zh-CN.md'],
	]],
	['interfaces', 'APIs and data', '接口与数据', [
		doc('API-v2-032', 'HTTP API v2 contract', 'HTTP API v2 契约'),
		doc('AUTH-v2', 'Relay authentication', 'Relay 鉴权'),
		doc('API', 'MacClient Local API', 'MacClient 本机接口'),
		['raster1', 'raster1 bitmap format', 'raster1 位图格式', 'Skills/paperang-local-raster/references/raster1.md'],
		['raster-skill', 'Local raster skill', '本地位图技能', 'Skills/paperang-local-raster/SKILL.md', null, 'zh-CN'],
	]],
	['protocols', 'Printer protocols', '打印机协议', [
		doc('PROTOCOL', 'P2 legacy BLE protocol', 'P2 传统 BLE 协议'),
		doc('USB-PROTOCOL', 'P2 USB protocol', 'P2 USB 协议'),
		['usb-research', 'Windows USB research log', 'Windows USB 研究记录', 'Windows/USB-RESEARCH.md', null, 'zh-CN'],
		doc('ACQUISITION', 'Protocol acquisition', '协议采集'),
		doc('RESEARCH', 'Device and image research', '设备与图像研究'),
		doc('CHARGING-AND-LED', 'Charging and LEDs', '充电与指示灯'),
		doc('ESP32-EMULATOR', 'ESP32 emulator', 'ESP32 模拟器'),
		doc('RELATED-PROJECTS', 'Related implementations', '相关实现'),
		doc('SOURCES', 'Sources and credits', '来源与致谢'),
	]],
	['develop', 'Development', '开发与维护', [
		doc('ARCHITECTURE', 'Architecture and packaging', '架构与打包'),
		doc('SWIFT-API', 'Swift modules', 'Swift 模块'),
		['windows-build', 'Build WinLite', 'WinLite 源码构建', 'Windows/README.md'],
		doc('WINDOWS-PORTING', 'Windows porting', 'Windows 移植'),
		doc('WINDOWS-RELEASE-ARTIFACTS', 'Windows release artifacts', 'Windows 发布物'),
		['contributing', 'Contributing', '参与开发', 'CONTRIBUTING.md', 'doc/CONTRIBUTING.zh-CN.md'],
		['local-tests', 'Local tests', '本地测试', 'tests/README.md'],
		['windows-tests', 'Windows local tests', 'Windows 本地测试', 'tests/windows/README.md'],
	]],
	['releases', 'Release history', '发布历史', [
		['changelog', 'Changelog', '变更记录', 'CHANGELOG.md', 'doc/CHANGELOG.zh-CN.md'],
		['release-v0.3.3-androidclient', 'AndroidClient 0.3.3', 'AndroidClient 0.3.3', 'doc/RELEASE-v0.3.3-androidclient.md', null, 'zh-CN'],
		doc('RELEASE-v0.3.3-macclient', 'MacClient 0.3.3', 'MacClient 0.3.3'),
		doc('RELEASE-v0.3.3-winlite', 'WinLite 0.3.3', 'WinLite 0.3.3'),
		doc('RELEASE-v0.3.2', 'Project 0.3.2', '项目 0.3.2'),
		doc('UPGRADE-v0.3.2', 'Upgrade and rollback (0.3.2)', '升级与回滚（0.3.2）'),
		doc('MACCLIENT-v0.3.1', 'MacClient 0.3.1 fix3', 'MacClient 0.3.1 fix3'),
		doc('OWNER-CUTOVER-v0.3.1', 'Owner key cutover (0.3.1)', '所有者私钥切换（0.3.1）'),
		doc('UPGRADE-v0.3.0', 'Upgrade and rollback (0.3.0)', '升级与回滚（0.3.0）'),
	]],
];

export const references = [
	['VERSIONS.json', 'Version registry', '版本登记'],
	['doc/openapi.json', 'OpenAPI definition', 'OpenAPI 定义'],
	['doc/p2-profile.json', 'P2 BLE profile', 'P2 BLE 配置'],
	['doc/p2-usb-profile.json', 'P2 USB profile', 'P2 USB 配置'],
	['doc/source-revisions.json', 'Upstream source revisions', '上游源码版本'],
	['doc/API-AUDIT-v0.3.2.json', 'API 0.3.2 audit record', 'API 0.3.2 审计记录'],
	['LICENSE', 'License', '许可证'],
	['NOTICE', 'Notices', '署名与声明'],
];
