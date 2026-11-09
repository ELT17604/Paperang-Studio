# opt1 code and interface review

[简体中文](OPTIMIZATION-opt1.zh-CN.md) · [Versions](../VERSIONS.json)

## Versions and scope

Compatible baseline **0.3.1** is unchanged. Current distribution versions are `0.3.1-macclient-opt1`, `0.3.1-linuxrelayserver-opt1`, `0.3.1-winlite-opt1` and `0.3.1-relayskill-opt1`. Relay API v2, Mac local API v1, templates v1 and raster1 schema1 retain their contracts. This is a local source/build update; it does not install a server, publish a release or establish new physical-print evidence.

## Reviewed changes

- Mac: removed the previous management panel's unused fields/type, a write-only private-content cache, an unused document wrapper and duplicate preference writes. Kept old preference reads for existing installations. Unified Bluetooth failures with the existing print error type.
- Mac remote printing: separate Tasks, Connection, Style and Management pages. The management view remains mounted while switching these tabs, so its independent C session remains usable for task previews. Leaving Remote Print still locks it. Forms use vertical groups and adaptive field controls; previews and ongoing tasks survive queue refreshes. Preview invalidation includes actual print settings. A queued descriptor no longer triggers an unauthorized background content read.
- Relay: removed unreachable v1 public/admin/receipt/queue/payload routes and their unused helpers. The live device state machine has an explicitly named internal v2 adapter. External v1 requests still return 410. Database migration and CLI key import remain supported.
- Web: removed permanently hidden legacy content/error/stage elements and their unused styles. Compact identity and quota summaries, expandable identity details, responsive sign-in/send controls, and 30-second foreground server/session refresh. Offline refresh failures block new submissions; status polling explicitly labels a stale result. Text and drawing use the existing submission flow.
- Versions: Mac and Windows update comparison accepts both `fixN` and `optN`. Within a matching baseline, initial/fix revisions precede opt revisions; counters compare numerically. Subsequent optimization releases should use `opt2`, etc., or a newer numeric baseline. Other distributions and prereleases remain excluded from stable client update checks. Older clients that only recognize fix tags require manual installation of this first opt1 build.
- RelaySkill: macOS Chinese font discovery now includes installed STHeiti and Arial Unicode paths.
- Windows: removed the assembly attribute that exposed internals only to the removed test assembly. Runtime and project versions use opt1; no native Windows build is claimed for this review.

Input and protocol validation, authorization, cookie/CSRF handling, bounded responses, claim privacy, paper/density checks, durable results, cancellation and no-replay behavior are retained. They participate in live workflows. This review does not claim that every line in every platform is exhaustively proven necessary.

## Regression source location

At the owner's request, eight regression source/project files were moved out of the shipping repository into sibling `PaperangStudio-Development/Optimization-opt1/Regression/`. SHA-256 checks verified the backup before moving. Logs and UI validation artifacts live in that external development directory. Git history also retains the original tests at `4393ae0`.

The repository CI now compiles/imports the shipped MCP adapter and checks version metadata. It no longer pretends to run removed test files. The owner-cutover script performs syntax preflight on the staged Python files rather than depending on bundled regression fixtures; pinned archive verification, active-claim refusal, backups and rollback remain intact. This preflight is not a substitute for release regression validation.

To reproduce the original test sources on another checkout, restore each path from commit `4393ae0` into an **external** regression directory using `git show 4393ae0:path/to/file`, then run with the checkout's production module directory on `PYTHONPATH`. Historical validation records name the original paths and reflect their original versions.

Example from the repository root, with an isolated Python environment containing Server requirements, MCP requirements and Pillow:

```sh
export PAPERANG_REGRESSION="../PaperangStudio-Development/Optimization-opt1/Regression"
PYTHONPATH=Server python -m unittest discover -s "$PAPERANG_REGRESSION/Server" -p 'test_*.py' -v
PYTHONPATH=Integrations/ChatGPT python -m unittest discover -s "$PAPERANG_REGRESSION/Integrations/ChatGPT" -p 'test_*.py' -v
PYTHONPATH=Skills/paperang-relay-print/scripts python -m unittest discover -s "$PAPERANG_REGRESSION/Skills/paperang-relay-print/scripts" -p 'test_*.py' -v
python scripts/check-versions.py
```

## Validation record

macOS arm64 release build and strict ad-hoc signature check; isolated native owner/C/M integration; 39 server regressions; 9 MCP regressions; 12 RelaySkill regressions; JavaScript and Python syntax; version metadata. Native SwiftUI pages inspected with offline example data. Browser text/drawing submissions and own-job tracking exercised against an isolated real relay, with no printer registered. A 390px viewport had no horizontal overflow. Server-stop feedback was checked separately.

No new physical P2 output, deployed Linux update, native Windows build/UI run or long-duration stability test is claimed. Windows changes are reviewed source changes. Bundles are ad-hoc signed, not notarized. No credentials or generated databases are included in the source repository.

## Windows follow-up — 2026-11-10

Windows x64 self-contained setup built from opt1. All 61 historical Windows regression checks passed in an external workspace. Real installed fix3-to-opt1 overlay upgrade passed: stable AppId/path, graceful shutdown, updated uninstall registration, byte-identical settings/templates/owner key and running new tray process. Final payload rebuilt without temporary test access. No physical print or long-duration soak was performed. See the Chinese companion for package details.
