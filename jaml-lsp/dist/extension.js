// VS Code client — launches the JAML language server and wires it to .jaml docs.
//
// The client is intentionally dumb: it spawns the stdio server (which any other
// LSP-capable editor can spawn the same way) and lets vscode-languageclient
// pump diagnostics/completion/hover/symbols. No language logic here either.
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { LanguageClient, TransportKind, } from "vscode-languageclient/node.js";
let client;
export function activate(context) {
    const here = path.dirname(fileURLToPath(import.meta.url));
    const serverModule = path.join(here, "server.js");
    const serverOptions = {
        run: { module: serverModule, transport: TransportKind.ipc },
        debug: { module: serverModule, transport: TransportKind.ipc },
    };
    const clientOptions = {
        documentSelector: [{ scheme: "file", language: "jaml" }],
    };
    client = new LanguageClient("jaml", "JAML Language Server", serverOptions, clientOptions);
    context.subscriptions.push({ dispose: () => void client?.stop() });
    void client.start();
}
export function deactivate() {
    return client?.stop();
}
//# sourceMappingURL=extension.js.map