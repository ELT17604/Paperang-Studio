# Remote task list and graphical layout editor

> v0.3.0: historical observations/screenshots below remain recorded evidence. Current relay authentication, history access, dependencies and validation status are defined by [API v2](AUTH-v2.md), [upgrade](UPGRADE-v0.3.0.zh-CN.md) and [matrix](FEATURE-MATRIX.md). Local API v1 and BLE/template records are separate contracts.

[Documentation](README.md) · [简体中文](LAYOUT-EDITOR.zh-CN.md)

## Task list and short IDs

The remote page shows task rows with sender, preview, submission time, state, and a short ID. Filter by state or search by full/short ID, sender, or text. Select a row to load its full text, metadata, outcome, and internal ID. Only queued tasks can be approved/rejected; completed or uncertain tasks are never automatically retried. For an intentional manual reprint, inspect the paper and use Copy to workspace.

New internal IDs are 40 lowercase hexadecimal characters generated from a SHA-256 digest of time and cryptographic random input, truncated to160 bits. They resemble commit IDs in format, but are not Git commits or hashes of the visitor's content. The database primary key rejects duplicates.

Every task also receives an independently assigned **12-character uppercase hexadecimal short ID**, formatted as `A1B2-C3D4-E5F6`. A unique SQLite index and collision retry prevent duplicates within that database. Short IDs are for display/printing; API paths still use full IDs. Existing UUID tasks remain readable and receive short IDs automatically during database migration. Separate installations may assign the same short ID; it is not a globally unique identifier or authentication secret.

Printing the short ID is optional and disabled initially. Built-in styles use the Short ID checkbox; custom layouts use the Task ID block or `{{id}}` placeholder. The complete internal ID remains in task details, not on paper by default.

The device list API returns up to100 active/recent summaries, prioritizing active tasks. Long text is marked `truncated` and loaded from the device detail endpoint before printing; a summary is never printed in place of full content. Automatic mode processes queued tasks oldest first, independently of the UI filter.

## Default automatic printing

Automatic printing is off on first use. Enable Default automatic printing on next connection to save that preference locally. Connecting then enables runtime automatic mode, but still requires a ready P2, the global paper switch, an idle print pipeline, and no unresolved remote job. Turning off global paper use disables runtime automatic printing. Failures pause it rather than replaying uncertain output. Saved templates and selected style are local settings; server credentials are not persisted.

## Graphical editor

Open Remote printing → Graphical layout editor. The editor works on a draft, using the selected full task or an offline sample. Draft changes do not affect automatic printing until Save and apply.

- **Template bar:** select, rename, duplicate, load a preset, import/export JSON, or delete a template.
- **Block list:** add/remove blocks, drag to reorder, or use up/down buttons.
- **Inspector:** enable a block, choose its content/font/size/bold/alignment, adjust horizontal margins and space before it.
- **Paper canvas:** click a block to select it; drag horizontally to adjust its margins and vertically to adjust preceding whitespace. Text flows as sequential blocks, so a long body pushes later fields instead of overlapping them.
- **Save and apply:** validates all templates, stores them in local app preferences, and selects Custom style. Cancel discards the draft.

Block types: title, body, sender, time, short task ID, IP, UA, browser metadata, divider, and custom text. Custom text supports `{{text}}`, `{{sender}}`, `{{id}}`, `{{time}}`, `{{ip}}`, `{{ua}}`, and `{{device}}`. Replacement happens once in the template; placeholder-looking text inside a visitor's message is not expanded.

Limits:30 saved templates,24 blocks/template, name80 characters, custom text2000 characters/block, font size8–96, margins0–120 dots, preceding gap0–200 rows. Imports are capped at64KiB and validated. The composed raster is fixed576 dots wide and at most1400 rows high. Each block is rendered with the shared renderer, then exact one-bit rows and white gaps are stacked; preview and print use the same composition method.

Custom templates control their own enabled fields; the built-in style checkboxes are disabled while Custom is selected. IP/UA/browser fields and task IDs are not enabled in the initial presets. Imported templates should be reviewed before automatic printing.

## Image tasks in custom templates

The Body block accepts both text and raster jobs. For an image task it contains the image, proportionally fitted to the block’s symmetric margins, then its optional caption (4 blank rows between them). Font/bold/alignment control the caption. The image uses one-bit nearest-neighbor scaling; margin 0 preserves its bytes. Reordering moves the whole body; disabling Body hides image and caption. All other blocks keep their existing rendering and spacing. Existing version-1 template JSON files work without migration. The editor, job preview, workspace copy and print path share the same template parts and final compositor. Built-in field checkboxes do not override custom block settings. Total height, including all gaps, remains capped at 1400 rows.

Client v0.2.3-fix1 uses sorted-key template signatures for both editor and task preview. Preview requests fetch full content on demand, show loading/failure state, and retain a valid generated image across unrelated polling/status updates. Changing task content or layout marks the previous preview stale until regenerated. This client fix needs no relay or website update.
