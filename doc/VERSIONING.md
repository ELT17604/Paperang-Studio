# Version naming and compatibility policy

## Independent 0.3.3 client releases

MacClient and WinLite have scoped 0.3.3 versions. LinuxRelayServer and the shared compatibility baseline remain 0.3.2 with Relay v2 and schema 1. The independent release tags are v0.3.3-macclient and v0.3.3-winlite. See the [MacClient release notes](RELEASE-v0.3.3-macclient.md) for paused support.

## v0.3.2 scoped exception (owner-directed)

The 0.3.2 baseline retains the documented distribution suffixes: `v0.3.2-macclient`, `v0.3.2-winlite`, `v0.3.2-linuxrelayserver`, and `v0.3.2-relayskill`. Runtime strings omit the leading v. No old opt/fix suffix is carried into the new baseline. Shared `targetVersion` is the numeric baseline; all implemented components now share 0.3.2. Product tag/Release `v0.3.2` contains platform-named assets; update discovery maps it to the installed platform only when its package exists. Distribution runtime parsing and historical fix/opt comparison remain distinct.

The owner explicitly requires retiring Mac local HTTP API v1 in this patch baseline. This compatibility exception returns 410, requires local callers to migrate to v2 and stable English job states, and does not change Relay v2, template/content schema 1 or physical protocol. Historical tags remain immutable. The final product tag is `v0.3.2`; platform suffixes remain in runtime and package names.


> **0.x policy approved for v0.3.0:** a coordinated MINOR baseline may break the relay contract before 1.0.0. All participants must upgrade together. MAJOR breaking rules apply from 1.0.0; fix notes never break contracts. Historical baseline was 0.3.1/API v2; the current baseline is 0.3.2; the 0.2.3 section below is historical.

[简体中文](VERSIONING.zh-CN.md) · [Feature implementation matrix](FEATURE-MATRIX.md) · [Release registry](../VERSIONS.json)

Use **`vMAJOR.MINOR.PATCH-distribution[-note]`**, for example **`v0.2.3-winlite-fix1`**. Tags, release titles and documentation use the leading `v`; runtime strings and VERSION files may omit it. Distribution IDs are stable lowercase ASCII: `macclient`, `linuxrelayserver`, `winlite`, `relayskill`, `winclient`, `linuxclient`. Do not introduce `win_lite` or mixed-case spellings in new releases.

The numeric baseline identifies one product generation and its compatibility contract. Matching baselines mean the declared shared relay API, content and template schemas interoperate, with equivalent semantics for implemented common features. It does not mean a relay server has a printer UI or a tray-only client has every full-client tool. Scope differences must be explicit in the matrix; an unimplemented mandatory feature blocks a full-client parity claim.

## Baseline and distribution revisions

- **MAJOR** changes for a breaking shared contract or incompatible product generation.
- **MINOR** changes for a coordinated new feature baseline. Implementations move to it only when their required features and interoperability checks pass.
- **PATCH** changes for a shared baseline correction requiring coordination across distributions, even if backward compatible.
- **`fixN`** identifies a distribution-only compatible correction or local improvement. Counters are local to each baseline/distribution and restart when the baseline changes. Never change API/schema semantics incompatibly under a fix note.
- `alphaN`, `betaN`, `rcN` are optional single prerelease notes. No note means the initial stable distribution release. Compare baselines, distributions and numeric counters separately, not lexicographically.

New tags are immutable. Existing names such as `0.2.3-fix1` and `0.2.3-win_lite` remain historical aliases; do not rewrite history or tags. [VERSIONS.json](../VERSIONS.json) records canonical names and actual runtime/source aliases. MacClient and LinuxRelayServer retain their deployed legacy runtime strings, mapped to canonical names in the registry; a Windows release does not silently rebuild or redeploy them. Planned distributions have no released version.

## Baseline 0.2.3 contract

Relay HTTP `v1`; Bearer roles; `text,raster1` negotiation; raster1 schema 1 and SHA-256; template JSON version 1; 576-dot/72-byte legacy P2 bitmaps; queued → claimed → printing → sent/unknown/rejected and separate manual `confirmed`. An ACK or `sent` never proves paper completion. Unknown/interrupted sends must not automatically replay. Runtime version labels are not protocol capability negotiation: use explicit API/schema/capability fields.

Windows font substitution, startup facilities and reconnect scheduling may vary within this contract. Fonts need not be pixel-identical. Credential defaults, automatic paper consent and persistence differences must be documented as distribution behavior rather than assumed parity.

## Release checklist

1. Update that distribution's VERSION/runtime metadata, `VERSIONS.json`, changelog and bilingual guide. Retain the baseline for a compatible distribution fix.
2. Update every changed feature's matrix status, exception reason, source and verification. Distinguish source review, simulation, hardware and user confirmation.
3. Test client/server compatibility, template round trips, state transitions and no-replay behavior. Full clients must meet their required matrix rows; WinLite exclusions stay explicit.
4. Run `python scripts/check-versions.py` and implementation tests. Verify packaging; exclude credentials, raw identifiers and build output; retain notices.
5. Use a unique tag/artifact, e.g. `v0.2.3-winlite-fix1` and `PaperangStudio-v0.2.3-winlite-fix1-win-x64-lite.zip`. Never overwrite another distribution's release or imply a planned client exists.

## optN revisions

`optN` identifies a compatible optimization revision. For stable update checks at the same baseline, initial/fix revisions precede opt revisions; opt counters compare numerically. After opt1, use opt2 or a newer numeric baseline. Historical tags are unchanged. See [opt1 review](OPTIMIZATION-opt1.md).
