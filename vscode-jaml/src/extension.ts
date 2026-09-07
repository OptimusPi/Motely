/**
 * VS Code client for Motely.Lsp + @jimbo chat participant.
 *
 * Language: Motely.Lsp over stdio (engine grammar only — no TS reimplementation).
 * Chat: @jimbo — slash engine paths + freeform LM tool loop (J4).
 */
import * as vscode from "vscode";
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";
import { registerJimboChat } from "./jimboChat";
import { resolveMotelyLsp } from "./motelyEngine";
import { registerMotelyTools } from "./motelyTools";
import { wasmVersion } from "./motelyWasm";

let client: LanguageClient | undefined;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  registerJimboChat(context);
  registerMotelyTools(context);

  void wasmVersion().then(
    (version) => vscode.window.setStatusBarMessage(`JAML: Motely WASM ${version} + @jimbo`, 4000),
    () => undefined,
  );

  // Not awaited: in the workspace-fallback path this is a `dotnet run`, so awaiting it holds
  // activation open for a full restore + build. Chat and tools do not depend on the server.
  void startLanguageServer(context).catch((err) => {
    const msg = err instanceof Error ? err.message : String(err);
    vscode.window.showWarningMessage(`JAML LSP: ${msg}`);
  });
}

async function startLanguageServer(context: vscode.ExtensionContext): Promise<void> {
  const server = resolveMotelyLsp(context);
  // Long-running server: no --diagnose flags.
  const serverOptions: ServerOptions = {
    run: { command: server.command, args: [...server.args], transport: TransportKind.stdio },
    debug: { command: server.command, args: [...server.args], transport: TransportKind.stdio },
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "jaml" }, { scheme: "untitled", language: "jaml" }],
    synchronize: {
      fileEvents: vscode.workspace.createFileSystemWatcher("**/*.jaml"),
    },
  };

  client = new LanguageClient(
    "jaml",
    "Motely JAML Language Server",
    serverOptions,
    clientOptions,
  );

  context.subscriptions.push(client);
  await client.start();
  vscode.window.setStatusBarMessage(`JAML: Motely.Lsp (${server.display}) + @jimbo + tools`, 4000);
}

export async function deactivate(): Promise<void> {
  if (client) {
    await client.stop();
    client = undefined;
  }
}
