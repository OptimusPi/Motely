# JAML (Motely) — VS Code

Real language support for `.jaml` files + **`@jimbo`** Copilot Chat participant (J0 scaffold).

| Concern | Owner |
|--------|--------|
| Parse / validate | `Motely` engine via `JamlConfigLoader` |
| Schema / vocab | Generated `JamlSchema` + engine enums |
| Protocol | `Motely.Lsp` (JSON-RPC 2.0 stdio) |
| Editor glue | this package (`vscode-languageclient`) |
| Chat | `@jimbo` — `src/jimboChat.ts` (J0: pong + slash stubs; tools later) |

There is no TypeScript reimplementation of the JAML grammar.

## @jimbo (chat)

In **Copilot Chat** (or VS Code Chat with a model):

```
@jimbo hi
@jimbo /validate
@jimbo /find
@jimbo /explain must vs should
```

| Phase | Status |
|-------|--------|
| J0 | Participant registered, stream + slash stubs |
| **J1** | `motely_validate_jaml` tool + `@jimbo /validate` → `Motely.Lsp --diagnose` |
| J2 | `motely_search_seeds` (real Motely collect) |

Agent chat can `#validateJaml` or auto-invoke the tool. Slash: `@jimbo /validate` with a `.jaml` focused.

F5 Extension Development Host → open Chat → `@jimbo`.

## Install (dev, from this repo)

```sh
# 1. Language server
dotnet build Motely.Lsp

# 2. Extension host
cd vscode-jaml
npm install
npm run compile
# F5 in VS Code: Run Extension (Extension Development Host)
```

With the MotelyJAML workspace open, the extension runs:

`dotnet run --project Motely.Lsp`

## Bundle a .vsix (ship)

```sh
dotnet publish Motely.Lsp -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o vscode-jaml/server
# win-x64 / linux-x64 as needed
cd vscode-jaml
npm run package          # npx @vscode/vsce package → jaml-language-support-*.vsix
code --install-extension jaml-language-support-*.vsix
```

## Settings

| Setting | Meaning |
|---------|---------|
| `jaml.serverPath` | Absolute path to `Motely.Lsp` (or `.exe`) |
| `jaml.trace.server` | `off` / `messages` / `verbose` LSP traffic |

## Smoke the server without VS Code

```sh
dotnet build Motely.Lsp -c Release
node Motely.Lsp/smoke-lsp.mjs Motely.Lsp/bin/Release/net10.0/Motely.Lsp
```
