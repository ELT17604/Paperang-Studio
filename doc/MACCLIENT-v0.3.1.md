# MacClient 0.3.1 fix3 upgrade

[简体中文](MACCLIENT-v0.3.1.zh-CN.md) · [User guide](USER-GUIDE.md)

Updated 2026-11-09 from upstream `9471293`. MacClient is **0.3.1-macclient-fix3**. RelayServer remains 0.3.1-linuxrelayserver-fix1 and WinLite remains 0.3.1-winlite-fix3. Relay API v2, local API v1, template v1 and raster1 schema1 remain unchanged.

## Connection and recovery

Choose the paired owner private-key JSON file in Remote Print, check the server address, then connect. The file must belong to that server. RSA-PSS proof obtains a short-lived session, renewed before expiry; a rejected session is renewed once. The stable fingerprint uses the paired ID and SHA-256 of SPKI, matching WinLite. Existing M credentials remain a compatibility option; an M key revoked during server owner cutover cannot be reused.

Active connection recovery and unfinished connection drafts are stored separately in Keychain. Known sent results survive disconnects; uncertain results are reported without automatic reprinting. Body access requires the current claim. Completed/private history needs an independent C management session. Settings and templates are frozen before a job is claimed.

## Settings and management

Valid print/template/UI settings and connection drafts autosave. Startup connection and remembered-printer reconnect are opt-in. Enabling either does not grant global paper consent. Configuration JSON export includes settings, templates, UI preferences and owner-file path, but excludes credentials, private-key contents, sessions, pending results and task bodies. Exported paths may reveal local directory names. Keep the original owner file separately.

C management supports credentials, identities, sessions, audit and text/raster history; select a row for full details. Lists use 25/50/100 rows and at most 12 cached summary pages. Older fixed-size server pages are adapted without losing rows. Create credentials for a new or existing identity; rename, rotate or revoke as permitted. Empty passphrase generates one; custom phrases have no complexity policy. Quota fields distinguish blank/inherit from zero; read the selected scope before writing it. Owner-file generation uses the server-issued paired ID and refuses overwriting a file. History exports as streamed JSONL. The management session locks after ten minutes and is independent of background reception.
