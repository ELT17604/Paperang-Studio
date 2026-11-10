# USB documentation review and change map

[USB reference](USB-PROTOCOL.md) · [USB 中文协议](USB-PROTOCOL.zh-CN.md)

Baseline: `v0.3.2`, reviewed 2026-11-10. This inventory covers every tracked Markdown/text/reStructuredText document at the baseline, including bilingual and historical pages. Files were loaded in full for heading/USB/protocol-reference inspection; protocol, acquisition, research, driver, Windows and contribution sections were read in detail. An inventory/hash is not a claim that unrelated server deployment procedures were rerun. Historical claims remain historical. Newly authored USB pages and the pre-existing untracked Windows/USB-RESEARCH.md are outside this baseline count.

Baseline documents: **89**. No AGENTS.md was found in the checkout. Local ignored work/ and third-party checkouts are outside this inventory.

## Decisions

- Extend the existing legacy protocol with a separate USB chapter; preserve BLE handshake/index evidence.
- Link USB documentation from both indexes, both main README entrypoints, Windows guides, protocol/research/acquisition/charging/source/architecture pages and Windows test directions.
- Keep API, Relay permissions, schema1, release versions and historical acceptance unchanged: the research tool is not a new shipping transport.
- Initial research added transport metadata only. Owner-directed follow-up now integrates native USB into0.3.3-winlite, with an explicit Windows-only patch exception; other distributions and shared contracts stay0.3.2.
- Keep raw identifiers and attempt artifacts under ignored work/; publish only redacted measurements in p2-usb-profile.json.
- Preserve uncertain device printing/completion/charging, ACK-only measurements and class-control failures explicitly.

## Baseline inventory

| Document | Bytes | SHA-256 | USB relevance |
| --- | ---: | --- | --- |
| [CHANGELOG.md](../CHANGELOG.md) | 13951 | `94f1a667ceca34806e5c48035701fc8c15ac80fa06891fee27c2a2bdef8f6046` | Windows / platform / entrypoint |
| [CONTRIBUTING.md](../CONTRIBUTING.md) | 2180 | `38ff0dd9a611e23dd15f7b5b23ec8101f749dddf9564fd6b252bdbc9a5e778c5` | Windows / platform / entrypoint |
| [Integrations/ChatGPT/README.md](../Integrations/ChatGPT/README.md) | 5809 | `aedcc05660e1d29a25039c954b3fbb8122b17ebdae84932878b1ae7d7f9a31be` | Windows / platform / entrypoint |
| [Integrations/ChatGPT/requirements.txt](../Integrations/ChatGPT/requirements.txt) | 28 | `4eede9b73122f5c0b7cce6cc23d0bd8b71a53e0f270d301d58c51a4be50da69a` | separate application/server/history contract |
| [README.md](../README.md) | 12972 | `10ea779541d551db286bcc085df837dad198f65e9596401c31b080a96863aa68` | Windows / platform / entrypoint |
| [Server/ADMIN-CONSOLE.md](../Server/ADMIN-CONSOLE.md) | 4349 | `7f8115cd8154d9dabb2bbc803a9202022629eed266eb5b290f56bc9e7f6c700e` | separate application/server/history contract |
| [Server/README.md](../Server/README.md) | 5611 | `e039935da3ec9789e1336d850a2a35e45522059585c4a2f894e2fe2b10e7ef87` | Windows / platform / entrypoint |
| [Server/macOS-Temporary/README.md](../Server/macOS-Temporary/README.md) | 909 | `aa3a8a12f3040fdefed5a54cd05295c411593f1de23b7c932676eb053699e7d4` | Windows / platform / entrypoint |
| [Server/requirements.txt](../Server/requirements.txt) | 43 | `529e2d7362a873ab95095d1e60e0737a411e538182762cb6a7c3d15b65a607da` | separate application/server/history contract |
| [Skills/paperang-relay-print/SKILL.md](../Skills/paperang-relay-print/SKILL.md) | 5333 | `55df732fcffac10f5a2d865289a9f5884f83b680e3c87c251781995f921b1a0d` | separate application/server/history contract |
| [Skills/paperang-relay-print/references/parameters.md](../Skills/paperang-relay-print/references/parameters.md) | 5214 | `7078ba979a49ad481cec61e8d519c04c5801e49bc7381958bab56ef841f7b9d5` | separate application/server/history contract |
| [Windows/README.md](../Windows/README.md) | 4285 | `cd3e186b27ae0ab4508562af26a7bc9d0f27d1d92cb359737aa504d97c26c70c` | Windows / platform / entrypoint |
| [assets/screenshots/README.md](../assets/screenshots/README.md) | 1410 | `19bcc34aec5085eee68903d0800a4b8b3af96a3cd6ccea622e146423ac77dd49` | Windows / platform / entrypoint |
| [doc/ACQUISITION.md](../doc/ACQUISITION.md) | 5984 | `a395a939c6c0321e4fe7022ee859db33d8e0c589e7048251ec64a9299a53423b` | protocol / device evidence |
| [doc/ACQUISITION.zh-CN.md](../doc/ACQUISITION.zh-CN.md) | 5576 | `ad0979c598aa99fdf54742210d777f1b14224eaaeff73ac695fa95fc9224a1a1` | protocol / device evidence |
| [doc/API-v2-032.md](../doc/API-v2-032.md) | 4924 | `7a39e491b4ec7a80b2ffa6d79d8ccf19eb661078664f8c70746aa9c5a2578a67` | separate application/server/history contract |
| [doc/API.md](../doc/API.md) | 10672 | `db0ff1b8c6d1080bf53d935d035245fe324f49f8bc1803678549e15f833171b1` | separate application/server/history contract |
| [doc/API.zh-CN.md](../doc/API.zh-CN.md) | 9636 | `33a7344529c8448dcefb4e1d4948198d74fcecded5111c215d939905bb6a4023` | separate application/server/history contract |
| [doc/ARCHITECTURE.md](../doc/ARCHITECTURE.md) | 4986 | `51cf39d67db3bc3307e6470c2dfe1f0723f3080c919c06f5a0979e9772b5ef83` | Windows / platform / entrypoint |
| [doc/ARCHITECTURE.zh-CN.md](../doc/ARCHITECTURE.zh-CN.md) | 4390 | `523accc3ba79ba1ef7445bc096f2ce0c9b2789251dffe1e29ba6f33ea877169a` | Windows / platform / entrypoint |
| [doc/AUTH-v2.md](../doc/AUTH-v2.md) | 9834 | `879323bab4bbc1996d2faa2f07145347a1b71428650d9800b5c712fdc0701684` | separate application/server/history contract |
| [doc/AUTH-v2.zh-CN.md](../doc/AUTH-v2.zh-CN.md) | 3984 | `af31edb3ed72d8842813ed5a5654d557ec8dc06f0e13221e280d42e4ce56282b` | separate application/server/history contract |
| [doc/CHANGELOG.zh-CN.md](../doc/CHANGELOG.zh-CN.md) | 10410 | `18955d1c95eb6809fcf8b4f444e9e6da3016b80b67595a74c95b202d41494b4f` | Windows / platform / entrypoint |
| [doc/CHARGING-AND-LED.md](../doc/CHARGING-AND-LED.md) | 3404 | `11fb8769e0eca82dec0594d55679de5e0e4277276d4584d7335fabb3d7687188` | protocol / device evidence |
| [doc/CHARGING-AND-LED.zh-CN.md](../doc/CHARGING-AND-LED.zh-CN.md) | 4299 | `352f6ef6b7a68d100ab22eee3b53bc150fd97fb0e5c1d43a76a32b489ab00010` | protocol / device evidence |
| [doc/CHATGPT.md](../doc/CHATGPT.md) | 3848 | `95d481c2cfdec0e23e7f48bc34f3cff3b0b1a834b547b4e2ab78d8896c63290b` | separate application/server/history contract |
| [doc/CHATGPT.zh-CN.md](../doc/CHATGPT.zh-CN.md) | 3760 | `51b358664d84d2ca2527a1eb97fdceb2a5b5f9fedf8e3778631d49aff5b17704` | separate application/server/history contract |
| [doc/CONTRIBUTING.zh-CN.md](../doc/CONTRIBUTING.zh-CN.md) | 1203 | `a257ec9a662206336cffdc3520e38d07db9f3b799fc04f0effc9ea29129b4b99` | Windows / platform / entrypoint |
| [doc/DOCUMENTATION-AUDIT-v0.3.0.md](../doc/DOCUMENTATION-AUDIT-v0.3.0.md) | 7630 | `218288e16200f041a2a5330216ee7d5a66dcd66a5c65e3cf445d6316f1dd1554` | separate application/server/history contract |
| [doc/ESP32-EMULATOR.md](../doc/ESP32-EMULATOR.md) | 3788 | `fe237142d0147619697a679c5dded028ac14d9a939596da13d1eab9e5e5b74a3` | protocol / device evidence |
| [doc/ESP32-EMULATOR.zh-CN.md](../doc/ESP32-EMULATOR.zh-CN.md) | 3950 | `dde4ca5900cf2c476112e8027bb62c6fd71da5ef4a3d5488e328a8e65d53bbda` | protocol / device evidence |
| [doc/FEATURE-MATRIX.md](../doc/FEATURE-MATRIX.md) | 7272 | `26b1b90c833c4dbb4b0202d34f17fd64f12c77d9489d3978189582b0dc00d0f5` | Windows / platform / entrypoint |
| [doc/FEATURE-MATRIX.zh-CN.md](../doc/FEATURE-MATRIX.zh-CN.md) | 5729 | `55d17ce50ae193db47b5b276d84f3737d0119a725e89143a35e6b2d70dac0e60` | Windows / platform / entrypoint |
| [doc/HANDOFF-QUOTAS-v0.3.1.zh-CN.md](../doc/HANDOFF-QUOTAS-v0.3.1.zh-CN.md) | 2055 | `3a777657aa58db25a7ef86a60e7268f91e420ef4a5d1a09a5f537936a688afc2` | separate application/server/history contract |
| [doc/HANDOFF-v0.3.0.zh-CN.md](../doc/HANDOFF-v0.3.0.zh-CN.md) | 9890 | `2f3ceb6e476f69bd9531c2a13260efbe5af1342829262e6c0ac91c3499c31b58` | separate application/server/history contract |
| [doc/INDEX.zh-CN.md](../doc/INDEX.zh-CN.md) | 2836 | `47f323560c6ba6e204198a1271c3677dc0b1919634cbabdda7eaef6f7023951e` | separate application/server/history contract |
| [doc/LAYOUT-EDITOR.md](../doc/LAYOUT-EDITOR.md) | 5848 | `89b383405ec1e62e4dbb2e9d449afae7b841dd49d6905b17ed1b2ab39db7148e` | separate application/server/history contract |
| [doc/LAYOUT-EDITOR.zh-CN.md](../doc/LAYOUT-EDITOR.zh-CN.md) | 4900 | `00e881e10254bfdc4bebb60889af24ede11fa3e1d3f380e814d9a09d2c7ebfd6` | separate application/server/history contract |
| [doc/LINUX-v0.3.0-VALIDATION.md](../doc/LINUX-v0.3.0-VALIDATION.md) | 3718 | `1304f78a2ac33c7eff4f1eab66a4bc146bcddd312c2cfd0139466d60bbbb3bb3` | separate application/server/history contract |
| [doc/MACCLIENT-v0.3.1.md](../doc/MACCLIENT-v0.3.1.md) | 3559 | `fd86a5235ad916a6cd0a27a621bda61951ee51247fef34398716fd7af13f03b0` | separate application/server/history contract |
| [doc/MACCLIENT-v0.3.1.zh-CN.md](../doc/MACCLIENT-v0.3.1.zh-CN.md) | 3063 | `0616111cf40928a15eb1c8e471c05279a98f3d592b44c3c8e9f825a0a6cbdd2b` | separate application/server/history contract |
| [doc/MACOS-TEMPORARY.zh-CN.md](../doc/MACOS-TEMPORARY.zh-CN.md) | 3565 | `3728024cf2e7dfbaac386f1060004c6edb3fd16676be744f662ee1cb76c73f12` | separate application/server/history contract |
| [doc/OPTIMIZATION-opt1.md](../doc/OPTIMIZATION-opt1.md) | 7917 | `77faf5c366a0a10c7eb3781314646093f7270afb0a0a2e731d90162d980262da` | separate application/server/history contract |
| [doc/OPTIMIZATION-opt1.zh-CN.md](../doc/OPTIMIZATION-opt1.zh-CN.md) | 6252 | `c540d7120b164ebbb2d6c80d2ee275e0b4317c0601c2cfbdfe0f9a9a4405ae52` | separate application/server/history contract |
| [doc/OWNER-CUTOVER-v0.3.1.zh-CN.md](../doc/OWNER-CUTOVER-v0.3.1.zh-CN.md) | 3810 | `4b5920a607ae6e5d585292865df936a86238560db1f89fda679abd724bb266d4` | separate application/server/history contract |
| [doc/PROTOCOL.md](../doc/PROTOCOL.md) | 9403 | `73349fdfd5ea65e3ae1ad2054b4c52b8d121254956fead322f807b9074f54d3c` | protocol / device evidence |
| [doc/PROTOCOL.zh-CN.md](../doc/PROTOCOL.zh-CN.md) | 10181 | `d8b6a383de3abc6f7fcf6ddbdb7afc70fa6f7de00c1e0e364df1ead14ce6ac45` | protocol / device evidence |
| [doc/README.md](../doc/README.md) | 3782 | `0a87b5ccb4372de6700b6df8eb6f7563521ba4cf4952d052cadb4975ea299c70` | Windows / platform / entrypoint |
| [doc/README.zh-CN.md](../doc/README.zh-CN.md) | 11424 | `0b4853429bafdaa375f9c9416cea73afb4a4678eb6e6ba2d2e7b0bb9a6ae1816` | Windows / platform / entrypoint |
| [doc/RELATED-PROJECTS.md](../doc/RELATED-PROJECTS.md) | 4111 | `22bccb986bfa878ec16e6d0377f5cc766bc1ae0bb86b3b9f1da1dfa10135cdaa` | protocol / device evidence |
| [doc/RELATED-PROJECTS.zh-CN.md](../doc/RELATED-PROJECTS.zh-CN.md) | 3950 | `f1894d35c28c772cb995ccd7103e9b3eda36896f39eac472182015e1d9ed23e9` | protocol / device evidence |
| [doc/RELAY-DATA.md](../doc/RELAY-DATA.md) | 1121 | `b74ae3bc67eb6bb47282b94c7e855f6422724c80c5c036dad294c91106dbf42b` | separate application/server/history contract |
| [doc/RELAY-DATA.zh-CN.md](../doc/RELAY-DATA.zh-CN.md) | 1060 | `2070c3ef7e2d548addee4b3ab7ec1655ff9ef67b9fe04d995c114ff275b989f0` | separate application/server/history contract |
| [doc/RELAY-SKILL.md](../doc/RELAY-SKILL.md) | 4410 | `f3d526ef90d76026068822224010405b0fc3f7e7962ee1dc4e459f5815441202` | separate application/server/history contract |
| [doc/RELAY-SKILL.zh-CN.md](../doc/RELAY-SKILL.zh-CN.md) | 3764 | `c405478c214cdb58810b8c6566fe684749cf01b555cb86f3489ccda5a5256613` | separate application/server/history contract |
| [doc/RELEASE-v0.3.2.md](../doc/RELEASE-v0.3.2.md) | 3490 | `14a12abac0823185163dc9fe694b7a2ca8d24b0c333f6a013a31093f57e54650` | separate application/server/history contract |
| [doc/REMOTE-PRINTING.md](../doc/REMOTE-PRINTING.md) | 1316 | `717fff117ba816cc0a711f3a5e13ea226ac164975671ab7e7543f329dc382a2a` | separate application/server/history contract |
| [doc/REMOTE-PRINTING.zh-CN.md](../doc/REMOTE-PRINTING.zh-CN.md) | 1219 | `7dc31473c5b2d2e9edcfaab22fcf95be086b71806e1680ad788861acf79b5896` | separate application/server/history contract |
| [doc/RESEARCH.md](../doc/RESEARCH.md) | 7462 | `af99b201bddbccfde34c5339840143790eadeb0b573f4de24615c354f3596e69` | protocol / device evidence |
| [doc/RESEARCH.zh-CN.md](../doc/RESEARCH.zh-CN.md) | 12834 | `dc36df5ead0b2ce05cb9c31f19c9c14a6b7c1b04d133eec493ff6670223cb632` | protocol / device evidence |
| [doc/RICH-MEDIA.md](../doc/RICH-MEDIA.md) | 7453 | `0e79d95b05595243f8bb0499d337ba9bdf11d1624aeb94ed1296896a26cd5c4c` | separate application/server/history contract |
| [doc/RICH-MEDIA.zh-CN.md](../doc/RICH-MEDIA.zh-CN.md) | 5888 | `edcdf531984eab002ad4900555488ec153dcea801de99887aa7a87ab245bfde7` | separate application/server/history contract |
| [doc/SOURCES.md](../doc/SOURCES.md) | 2231 | `3cadb5f9d711f8255db5d8a2b47a108d30df7d7f0b12be7abc8e6685ef22cbc3` | protocol / device evidence |
| [doc/SOURCES.zh-CN.md](../doc/SOURCES.zh-CN.md) | 2251 | `91fc40a4aa07887d568acf4afd76000cc576437ae5be27c96f1344448d5fbd53` | protocol / device evidence |
| [doc/SWIFT-API.md](../doc/SWIFT-API.md) | 4657 | `658096f910196b2f44b8c5e3bb1764ce34b60f6bca0adbcb9be7b8a0bd29fe28` | separate application/server/history contract |
| [doc/SWIFT-API.zh-CN.md](../doc/SWIFT-API.zh-CN.md) | 4249 | `49c848a8a8f7e24f3e64160422a5144c998a6d60542f48043fdc4b7daeba4aab` | separate application/server/history contract |
| [doc/TERMINAL.md](../doc/TERMINAL.md) | 3153 | `5521b89ad1c723fb659524a2068608d68d276c2464ce077c8dd26e0845c14228` | separate application/server/history contract |
| [doc/TERMINAL.zh-CN.md](../doc/TERMINAL.zh-CN.md) | 2977 | `7523012e61353489d5978ce0f2a03bdef0f6b1be4b006497cec3b2e5f7a4bd35` | separate application/server/history contract |
| [doc/TRAY-MODE.md](../doc/TRAY-MODE.md) | 4987 | `ff66ef86d300cf02979006bebac3c1e7a2cc05a4b347fd6dae5e01fc880428bc` | separate application/server/history contract |
| [doc/TRAY-MODE.zh-CN.md](../doc/TRAY-MODE.zh-CN.md) | 4249 | `97c8f5abb256fab5a3bde2b65f51941971728f8c13267994c4856d3784f29ed2` | separate application/server/history contract |
| [doc/UPGRADE-v0.3.0.md](../doc/UPGRADE-v0.3.0.md) | 3675 | `d6774c156d490f4dc18c27aca980c118cebe4725f822eca5c9c99f14f0221d59` | separate application/server/history contract |
| [doc/UPGRADE-v0.3.0.zh-CN.md](../doc/UPGRADE-v0.3.0.zh-CN.md) | 5070 | `2d10de5ea9eb98c526856b5d9a32c50f640bafd198642c65c92253303c4eb24c` | separate application/server/history contract |
| [doc/UPGRADE-v0.3.2.md](../doc/UPGRADE-v0.3.2.md) | 5240 | `e09602736586c1017f36ba7059500bffd4a4790f1a3e46b75637e305802d6b3b` | separate application/server/history contract |
| [doc/USER-GUIDE.md](../doc/USER-GUIDE.md) | 6248 | `d8ebf333548630bec3acac1f0b90f8e2fa6eee2b45e2b84a302fd12de5c99c58` | separate application/server/history contract |
| [doc/USER-GUIDE.zh-CN.md](../doc/USER-GUIDE.zh-CN.md) | 7112 | `90ee1eccd94685d00e289cc903383c360613475674574337717f8eebbb647403` | separate application/server/history contract |
| [doc/VALIDATION-v0.3.0.md](../doc/VALIDATION-v0.3.0.md) | 2602 | `15f2bc161d12ec1675e91aae3abec4dac5673e800ef8f6e65888b28b3e619c64` | separate application/server/history contract |
| [doc/VALIDATION-v0.3.1.zh-CN.md](../doc/VALIDATION-v0.3.1.zh-CN.md) | 2220 | `4463540cf5089a66d80c43dd17d9cca8cc28cb117fd2f9ea11489d367fc96897` | separate application/server/history contract |
| [doc/VALIDATION-v0.3.2-macos.md](../doc/VALIDATION-v0.3.2-macos.md) | 5335 | `c276453dd2af1fece0a133b1a23eb7c46eaad787bf8d65e0516553bc2c6dc98d` | separate application/server/history contract |
| [doc/VALIDATION-v0.3.2-ubuntu.md](../doc/VALIDATION-v0.3.2-ubuntu.md) | 6854 | `9dd2d474de1631d419d9247e8551fffd465c91d1b28ce4d4ad43f7342d521d8f` | separate application/server/history contract |
| [doc/VALIDATION-v0.3.2-windows.md](../doc/VALIDATION-v0.3.2-windows.md) | 10196 | `e9d707dc568ab510c204ddb4a2b87fd03d7f289ae01f46165b110095e257c7db` | separate application/server/history contract |
| [doc/VERSIONING.md](../doc/VERSIONING.md) | 5669 | `a1eff6e6ad69599a493d996411a03674bd6637b00624fb47924dda24fdda0b80` | Windows / platform / entrypoint |
| [doc/VERSIONING.zh-CN.md](../doc/VERSIONING.zh-CN.md) | 4925 | `07c1a8c7f985a6b3d6156d8ad05dda478fbe08a884cf0e4f560eb91570e5a604` | Windows / platform / entrypoint |
| [doc/WINDOWS-PORTING.md](../doc/WINDOWS-PORTING.md) | 2086 | `384834cab0509dd635dea7a46241bf8649a7ae0e44a29805fad9a0839e5e0aa0` | Windows / platform / entrypoint |
| [doc/WINDOWS-PORTING.zh-CN.md](../doc/WINDOWS-PORTING.zh-CN.md) | 5595 | `4de7d5312754f9f7ddb078cf7115393eca0a36cc8f100aa6437aabcc178fc978` | Windows / platform / entrypoint |
| [doc/WINDOWS-RELEASE-ARTIFACTS.zh-CN.md](../doc/WINDOWS-RELEASE-ARTIFACTS.zh-CN.md) | 1829 | `e6dac792b3dc597e1a9e3b260519133ca15edc4f017117015c24f1da41370adc` | Windows / platform / entrypoint |
| [doc/WINDOWS.md](../doc/WINDOWS.md) | 12310 | `d08166893b92bb0248dc532b4a8b58204e61cc69ea4a7cc113eecdef18b5b8cf` | Windows / platform / entrypoint |
| [doc/WINDOWS.zh-CN.md](../doc/WINDOWS.zh-CN.md) | 10519 | `7578e75e73c9abd71a5646c4810225effb717c78b0a16dff9e69be40934db633` | Windows / platform / entrypoint |
| [tests/README.md](../tests/README.md) | 3870 | `428be42a5f2da26bb13c706ac112f5e30462ef150ecb470c13a0407223c74feb` | Windows / platform / entrypoint |
| [tests/windows/README.md](../tests/windows/README.md) | 3983 | `0c7c9c7fa31fb6ff2ed2807ca00d4a002a170162c060bc03b39f4035892ea333` | Windows / platform / entrypoint |

## Native integration follow-up

[WinLite0.3.3 release/upgrade](RELEASE-v0.3.3-winlite.md) and [validation](VALIDATION-v0.3.3-windows.md) supersede research-only integration status. Current Windows guides, READMEs, matrices, policy, registry, installer metadata and test/CI instructions are synchronized; historical device/release observations are preserved. Cross-transport CRC seed0 sharing is newly measured, rather than inferred.
