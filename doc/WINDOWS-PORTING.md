# Windows porting notes

> v0.3.0: historical observations/screenshots below remain recorded evidence. Current relay authentication, history access, dependencies and validation status are defined by [API v2](AUTH-v2.md), [upgrade](UPGRADE-v0.3.0.zh-CN.md) and [matrix](FEATURE-MATRIX.md). Local API v2 and BLE/template records are separate contracts.

[简体中文](WINDOWS-PORTING.zh-CN.md) · [Windows guide](WINDOWS.md)

The Windows adaptation is based on macOS revision `b1788b07aacb8c0cff7f86a884e58fb4ad8afb32`. It adds `Windows/` without replacing the Swift/C targets, original server, API clients or icon assets.

| Original module | Windows implementation |
| --- | --- |
| ProtocolCore/Protocol.swift | Core/Protocol.cs and P2Session.cs |
| Bluetooth.swift and Workspace.swift print path | WinLite/BlePrinter.cs and Core/P2Session.cs |
| Relay.swift and RelayHealth.swift | RelayClient.cs and RelayEngine.cs |
| RelayLayout.swift and PrintCore rendering | Templates.cs, TemplateEditor.cs and NoteRenderer.cs |
| TrayMode.swift | WinForms ApplicationContext and NotifyIcon |
| Server/relay.py | Unmodified; exercised by Windows/tests/relay_integration.py |

Windows-specific additions are current-user DPAPI, atomic pending-result persistence, optional HKCU Run registration and single-instance signaling. A selected Bluetooth address replaces the Mac-specific CoreBluetooth UUID. Fonts use Windows substitutes while JSON retains the macOS font names.

The Windows scope does not run the macOS workspace, PTY, engineering writes, local HTTP API or administrator-key manager. Automatic print consent is configurable and follows the requested Windows tray behavior; no macOS consent default was changed.

See [verification and research](WINDOWS.md#build-and-verification) for the 39 checks, original-server integration, source references and actual P2 paper confirmation. Windows UUID-filtered service discovery returned Success with no FF00 entry on the tested stack; full discovery returned it. That observed difference is why the backend selects FF00 after full enumeration.
