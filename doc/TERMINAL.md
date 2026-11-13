# Physical paper terminal

[Documentation](README.md) · [简体中文](TERMINAL.zh-CN.md)

The terminal creates a real macOS controlling PTY (`/dev/ttys…`). The bundled `PaperangPTYHost` starts `/bin/zsh` in a new session. Commands, `cd`, environment variables, jobs, and interactive answers share one shell. The default is `zsh -f -i`, without the user's `.zshrc`; loading the profile is optional before startup. Commands run with the current user's permissions.

## Operation

1. Select a working directory and terminal size: 20–200 columns and 5–100 rows.
2. Start the terminal. Enter commands or answers and press Return. Ctrl-C, Ctrl-D, Ctrl-Z, Tab, and arrow controls send real terminal bytes.
3. Connect the P2 and enable the sidebar's global paper switch.
4. Enable automatic output printing or print the buffer manually. The default idle delay is 3 seconds; text uses Menlo 12, left alignment, density 100. Font size is adjustable from 8–18.
5. Continuous batches default to zero extra feed. At the end, feed 150 lines from the workspace for tearing off the strip. Per-batch terminal feed is adjustable from 0–300 lines.

Neither the shell nor automatic printing starts automatically. Stopping the shell preserves pending output. Before starting another shell, handle the previous buffer and in-flight print job. Turning off global paper use disables automatic printing and stops an active print send; already buffered printer data remains irreversible.

## Screen and paper behavior

`TERM=dumb` is intentional. The text normalizer handles split UTF-8, newlines, carriage-return overwrite, backspace, tabs, and limited ANSI controls; color and OSC content are omitted. This is a line/paper terminal, not a full-screen VT100 emulator for `vim` or `top`. Printed text cannot be erased, and editing an already printed prompt may produce repeated content in later batches.

Lines are capped at 512 characters, the transcript at 1500 lines, and the visible screen at its last 64,000 characters. Each print submission takes at most 1200 characters. Failed output is returned to the pending buffer and automatic printing pauses; inspect the paper before deciding what to retry.

Automatic printing pauses above 12,000 pending characters. A PTY session stops after 1 MB of total output. Each input is capped at 8192 bytes and the underlying write queue at 64 KiB. Export a transcript explicitly when needed; this is not a persistent unlimited log.

**Private input** uses a secure field, drops PTY output while active, clears pending output, and disables automatic printing. Hidden output is not replayed afterward. Existing paper or exported files are unaffected. General engineering logs record PTY path, PID, and exit events, not command text.
