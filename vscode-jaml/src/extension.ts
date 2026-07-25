/**
 * VS Code client for Motely.Lsp.
 *
 * This extension does not reimplement JAML. It registers the language id, then
 * starts the real C# language server (Motely.Lsp) over stdio via
 * vscode-languageclient. Diagnostics, hover, and completion all come from the
 * Motely engine through that process.
 */
import * as fs from "node:fs";
import * as path from "node:path";
import * as vscode from "vscode";
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";

let client: LanguageClient | undefined;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  const server = resolveServer(context);
  const serverOptions: ServerOptions = {
    run: { command: server.command, args: server.args, transport: TransportKind.stdio },
    debug: { command: server.command, args: server.args, transport: TransportKind.stdio },
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
  vscode.window.setStatusBarMessage(`JAML: Motely.Lsp (${server.display})`, 4000);
}

export async function deactivate(): Promise<void> {
  if (client) {
    await client.stop();
    client = undefined;
  }
}

interface ResolvedServer {
  command: string;
  args: string[];
  display: string;
}

/**
 * 1. `jaml.serverPath` when set
 * 2. Bundled `server/Motely.Lsp` next to the extension (publish output)
 * 3. Workspace `dotnet run --project Motely.Lsp` (repo checkout)
 */
function resolveServer(context: vscode.ExtensionContext): ResolvedServer {
  const configured = vscode.workspace.getConfiguration("jaml").get<string>("serverPath")?.trim();
  if (configured) {
    if (!fs.existsSync(configured)) {
      throw new Error(`jaml.serverPath does not exist: ${configured}`);
    }
    return { command: configured, args: [], display: configured };
  }

  const bundledName = process.platform === "win32" ? "Motely.Lsp.exe" : "Motely.Lsp";
  const bundled = path.join(context.extensionPath, "server", bundledName);
  if (fs.existsSync(bundled)) {
    return { command: bundled, args: [], display: "bundled" };
  }

  const folder = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
  if (folder) {
    const csproj = path.join(folder, "Motely.Lsp", "Motely.Lsp.csproj");
    if (fs.existsSync(csproj)) {
      return {
        command: "dotnet",
        args: ["run", "--project", csproj, "--no-launch-profile"],
        display: "dotnet run Motely.Lsp",
      };
    }
  }

  throw new Error(
    "Motely.Lsp not found. Set jaml.serverPath, publish Motely.Lsp into vscode-jaml/server/, " +
      "or open the MotelyJAML workspace so `dotnet run --project Motely.Lsp` can start.",
  );
}
