// VS Code client — launches the JAML language server and wires it to .jaml docs.
//
// The client is intentionally dumb: it spawns the stdio server (which any other
// LSP-capable editor can spawn the same way) and lets vscode-languageclient
// pump diagnostics/completion/hover/symbols. No language logic here either.

import * as path from "node:path";
import { fileURLToPath } from "node:url";
import type { ExtensionContext } from "vscode";
import {
  LanguageClient,
  type LanguageClientOptions,
  type ServerOptions,
  TransportKind,
} from "vscode-languageclient/node.js";

let client: LanguageClient | undefined;

export function activate(context: ExtensionContext): void {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const serverModule = path.join(here, "server.js");

  const serverOptions: ServerOptions = {
    run: { module: serverModule, transport: TransportKind.ipc },
    debug: { module: serverModule, transport: TransportKind.ipc },
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "jaml" }],
  };

  client = new LanguageClient(
    "jaml",
    "JAML Language Server",
    serverOptions,
    clientOptions
  );

  context.subscriptions.push({ dispose: () => void client?.stop() });
  void client.start();
}

export function deactivate(): Thenable<void> | undefined {
  return client?.stop();
}
