import * as path from "node:path";
import * as vscode from "vscode";
import { LanguageClient, ServerOptions, TransportKind } from "vscode-languageclient/node";

let client: LanguageClient | null = null;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  const extPath = context.extensionPath;

  const serverModule = path.join(extPath, "dist", "server.js");
  client = new LanguageClient(
    "jamlLanguageServer",
    "JAML Language Server",
    {
      run: { module: serverModule, transport: TransportKind.ipc },
      debug: { module: serverModule, transport: TransportKind.ipc },
    },
    { documentSelector: [{ scheme: "file", language: "jaml" }, { scheme: "file", language: "jummy" }] }
  );
  await client.start();
  context.subscriptions.push({ dispose: () => void client?.stop() });
}

export async function deactivate(): Promise<void> {
  if (client) {
    await client.stop();
    client = null;
  }
}
