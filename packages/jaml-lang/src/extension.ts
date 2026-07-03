import * as path from "node:path";
import * as vscode from "vscode";
import {
  LanguageClient,
  type LanguageClientOptions,
  type ServerOptions,
  TransportKind,
} from "vscode-languageclient/node.js";

let client: LanguageClient | undefined;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  // The server is bundled into this extension's dist folder so the VSIX is
  // self-contained and doesn't rely on a sibling jaml-lsp package at runtime.
  const serverModule = context.asAbsolutePath(path.join("dist", "server.mjs"));

  const serverOptions: ServerOptions = {
    run: { module: serverModule, transport: TransportKind.stdio },
    debug: { module: serverModule, transport: TransportKind.stdio },
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "jaml" }],
    synchronize: {
      fileEvents: vscode.workspace.createFileSystemWatcher("**/*.jaml"),
    },
  };

  client = new LanguageClient(
    "jamlLanguageServer",
    "JAML Language Server",
    serverOptions,
    clientOptions,
  );

  context.subscriptions.push({
    dispose: () => {
      void client?.stop();
    },
  });

  await client.start();
}

export async function deactivate(): Promise<void> {
  await client?.stop();
}
