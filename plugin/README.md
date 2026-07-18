# JAML plugin for Claude Code

Live JAML diagnostics, hover, and completion inside Claude Code, served by `Motely.Lsp` —
the engine's own parser and generated grammar answering over stdio. Edit a `.jaml` file and
Claude sees the same errors the engine would raise, positioned on the offending token.

## Build the server into the plugin

Publish a self-contained single-file binary for your platform into `plugin/server/`:

```sh
dotnet publish Motely.Lsp -c Release -r win-x64  --self-contained -p:PublishSingleFile=true -o plugin/server
# linux-x64 / osx-arm64 / osx-x64 for other machines
```

`${CLAUDE_PLUGIN_ROOT}/server/Motely.Lsp` in `.lsp.json` resolves to `Motely.Lsp.exe` on
Windows automatically.

## Try it

```sh
claude --plugin-dir ./plugin
```

Open any file under `JamlFilters/` and typo a key — the squiggle lands on the typo, with the
loader's own message. `/reload-plugins` picks up a freshly published server.

## Notes

- `restartOnCrash`/`shutdownTimeout` stay unset on purpose: Claude Code older than v2.1.205
  silently skips a server that sets them.
- The server logs to stderr only; stdout is the protocol channel. `claude --debug` shows both.
