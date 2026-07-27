/**
 * Resolve and invoke the Motely.Lsp binary for one-shot engine jobs (not the
 * long-running language server session). Same resolution order as LSP start.
 */
import { spawn } from "node:child_process";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";
import * as vscode from "vscode";

export interface MotelyProcess {
  command: string;
  args: string[];
  display: string;
}

export interface EngineDiagnostic {
  message: string;
  code: string;
  severity: string;
  startLine: number;
  startColumn: number;
  endLine: number;
  endColumn: number;
}

export interface DiagnoseResult {
  ok: boolean;
  diagnostics: EngineDiagnostic[];
  raw: string;
  exitCode: number;
  via: string;
}

/**
 * 1. `jaml.serverPath`
 * 2. Bundled `server/Motely.Lsp`
 * 3. Workspace `dotnet run --project Motely.Lsp`
 */
export function resolveMotelyLsp(context: vscode.ExtensionContext): MotelyProcess {
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
        args: ["run", "--project", csproj, "--no-launch-profile", "--"],
        display: "dotnet run Motely.Lsp",
      };
    }
  }

  throw new Error(
    "Motely.Lsp not found. Set jaml.serverPath, publish Motely.Lsp into vscode-jaml/server/, " +
      "or open the MotelyJAML workspace.",
  );
}

/** Run engine diagnose on text or an absolute file path. */
export async function diagnoseJaml(
  context: vscode.ExtensionContext,
  input: { jamlText?: string; filePath?: string },
): Promise<DiagnoseResult> {
  const proc = resolveMotelyLsp(context);
  let filePath = input.filePath?.trim();
  let tempPath: string | undefined;

  if (!filePath && input.jamlText !== undefined) {
    tempPath = path.join(os.tmpdir(), `motely-validate-${Date.now()}.jaml`);
    fs.writeFileSync(tempPath, input.jamlText, "utf8");
    filePath = tempPath;
  }

  if (!filePath) {
    throw new Error("Provide jamlText or filePath.");
  }
  if (!fs.existsSync(filePath)) {
    throw new Error(`File not found: ${filePath}`);
  }

  try {
    const args = [...proc.args, "--diagnose", filePath];
    const { stdout, stderr, code } = await run(proc.command, args);
    let diagnostics: EngineDiagnostic[] = [];
    const raw = stdout.trim();
    if (raw) {
      try {
        diagnostics = JSON.parse(raw) as EngineDiagnostic[];
      } catch {
        throw new Error(
          `Motely.Lsp --diagnose returned non-JSON (exit ${code}): ${raw || stderr}`,
        );
      }
    }
    return {
      ok: code === 0 && diagnostics.length === 0,
      diagnostics,
      raw,
      exitCode: code ?? 1,
      via: proc.display,
    };
  } finally {
    if (tempPath) {
      try {
        fs.unlinkSync(tempPath);
      } catch {
        /* ignore */
      }
    }
  }
}

function run(
  command: string,
  args: string[],
): Promise<{ stdout: string; stderr: string; code: number | null }> {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { stdio: ["ignore", "pipe", "pipe"] });
    let stdout = "";
    let stderr = "";
    child.stdout.on("data", (c: Buffer) => {
      stdout += c.toString("utf8");
    });
    child.stderr.on("data", (c: Buffer) => {
      stderr += c.toString("utf8");
    });
    child.on("error", reject);
    child.on("close", (code) => resolve({ stdout, stderr, code }));
  });
}

export function formatDiagnoseMarkdown(result: DiagnoseResult, label: string): string {
  if (result.ok) {
    return [
      `**JAML valid** (Motely engine via \`${result.via}\`)`,
      "",
      `Source: \`${label}\``,
    ].join("\n");
  }
  const rows = result.diagnostics.map(
    (d) =>
      `| L${d.startLine + 1}:${d.startColumn + 1} | ${d.severity} | \`${d.code}\` | ${escapePipes(d.message)} |`,
  );
  return [
    `**JAML invalid** — ${result.diagnostics.length} issue(s) (via \`${result.via}\`)`,
    "",
    `Source: \`${label}\``,
    "",
    "| Loc | Sev | Code | Message |",
    "|-----|-----|------|---------|",
    ...rows,
  ].join("\n");
}

function escapePipes(s: string): string {
  return s.replace(/\|/g, "\\|").replace(/\n/g, " ");
}
