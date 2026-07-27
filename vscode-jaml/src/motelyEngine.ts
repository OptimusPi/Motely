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

export interface ScoredSeed {
  seed: string;
  score: number;
}

export interface SearchResult {
  seeds: ScoredSeed[];
  collectN: number;
  via: string;
  exitCode: number;
  stdout: string;
  stderr: string;
  /** Temp filter path used so Motely.CLI does not rewrite the user's file. */
  filterPath: string;
  timedOut: boolean;
}

/**
 * 1. `jaml.cliPath`
 * 2. Bundled `server/Motely.CLI` next to the extension
 * 3. Workspace `dotnet run --project Motely.CLI`
 */
export function resolveMotelyCli(context: vscode.ExtensionContext): MotelyProcess {
  const configured = vscode.workspace.getConfiguration("jaml").get<string>("cliPath")?.trim();
  if (configured) {
    if (!fs.existsSync(configured)) {
      throw new Error(`jaml.cliPath does not exist: ${configured}`);
    }
    return { command: configured, args: [], display: configured };
  }

  const bundledName = process.platform === "win32" ? "Motely.CLI.exe" : "Motely.CLI";
  const bundled = path.join(context.extensionPath, "server", bundledName);
  if (fs.existsSync(bundled)) {
    return { command: bundled, args: [], display: "bundled CLI" };
  }

  const folder = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
  if (folder) {
    const csproj = path.join(folder, "Motely.CLI", "Motely.CLI.csproj");
    if (fs.existsSync(csproj)) {
      return {
        command: "dotnet",
        args: ["run", "--project", csproj, "-c", "Release", "--no-launch-profile", "--"],
        display: "dotnet run Motely.CLI",
      };
    }
  }

  throw new Error(
    "Motely.CLI not found. Set jaml.cliPath, publish Motely.CLI into vscode-jaml/server/, " +
      "or open the MotelyJAML workspace.",
  );
}

/**
 * Real Motely search: `--jaml <temp> --collect N -q`.
 * Always copies the filter to a temp file so CLI seed-rewrite does not touch the user's path.
 */
export async function searchSeeds(
  context: vscode.ExtensionContext,
  input: { jamlText?: string; filePath?: string; collectN?: number },
): Promise<SearchResult> {
  const collectN = Math.max(1, Math.min(input.collectN ?? 1, 100));
  const proc = resolveMotelyCli(context);

  let sourceText: string | undefined = input.jamlText;
  if (!sourceText && input.filePath?.trim()) {
    const p = input.filePath.trim();
    if (!fs.existsSync(p)) {
      throw new Error(`File not found: ${p}`);
    }
    sourceText = fs.readFileSync(p, "utf8");
  }
  if (sourceText === undefined) {
    throw new Error("Provide jamlText or filePath for search.");
  }

  // Validate first so we don't burn a long search on garbage.
  const diag = await diagnoseJaml(context, { jamlText: sourceText });
  if (!diag.ok) {
    const first = diag.diagnostics[0]?.message ?? "invalid JAML";
    throw new Error(`JAML invalid — fix with validate first. ${first}`);
  }

  const tempPath = path.join(os.tmpdir(), `motely-search-${Date.now()}.jaml`);
  fs.writeFileSync(tempPath, sourceText, "utf8");

  const timeoutMs =
    vscode.workspace.getConfiguration("jaml").get<number>("searchTimeoutMs") ?? 120_000;

  try {
    const args = [
      ...proc.args,
      "--jaml",
      tempPath,
      "--collect",
      String(collectN),
      "-q",
      "--threads",
      "4",
    ];
    const { stdout, stderr, code, timedOut } = await run(proc.command, args, timeoutMs);
    if (timedOut) {
      throw new Error(
        `Motely.CLI timed out after ${timeoutMs}ms (via ${proc.display}). Raise jaml.searchTimeoutMs or simplify the filter.`,
      );
    }
    if (code !== 0 && code !== null) {
      throw new Error(
        `Motely.CLI exited ${code} (via ${proc.display}). ${stderr.trim() || stdout.trim() || "no output"}`,
      );
    }
    const seeds = parseScoredSeeds(stdout);
    return {
      seeds,
      collectN,
      via: proc.display,
      exitCode: code ?? 0,
      stdout,
      stderr,
      filterPath: tempPath,
      timedOut: false,
    };
  } finally {
    try {
      fs.unlinkSync(tempPath);
    } catch {
      /* ignore */
    }
  }
}

/** Parse Motely.CLI CSV-ish result lines: `SEED, score,...` */
export function parseScoredSeeds(stdout: string): ScoredSeed[] {
  const out: ScoredSeed[] = [];
  const seen = new Set<string>();
  for (const line of stdout.split(/\r?\n/)) {
    const m = line.match(/^([1-9A-Z]{1,8}),\s*(-?\d+)/i);
    if (!m) {
      continue;
    }
    const seed = m[1].toUpperCase();
    if (seen.has(seed)) {
      continue;
    }
    seen.add(seed);
    out.push({ seed, score: Number(m[2]) });
  }
  return out;
}

export function formatSearchMarkdown(result: SearchResult, label: string): string {
  if (result.seeds.length === 0) {
    return [
      `**No seeds found** (Motely via \`${result.via}\`, collect ${result.collectN})`,
      "",
      `Filter: \`${label}\``,
      "",
      "Engine ran; zero matches in the collect sweep. Try a looser filter or higher collect N.",
    ].join("\n");
  }
  const rows = result.seeds.map((s) => `| \`${s.seed}\` | ${s.score} |`);
  const listed = result.seeds.map((s) => s.seed).join(", ");
  return [
    `**Found ${result.seeds.length} seed(s)** (Motely via \`${result.via}\`, collect ${result.collectN}; SIMD may overshoot)`,
    "",
    `Filter: \`${label}\``,
    "",
    "| Seed | Score |",
    "|------|------:|",
    ...rows,
    "",
    "```",
    listed,
    "```",
  ].join("\n");
}

function run(
  command: string,
  args: string[],
  timeoutMs?: number,
): Promise<{ stdout: string; stderr: string; code: number | null; timedOut: boolean }> {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { stdio: ["ignore", "pipe", "pipe"] });
    let stdout = "";
    let stderr = "";
    let timedOut = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    if (timeoutMs && timeoutMs > 0) {
      timer = setTimeout(() => {
        timedOut = true;
        child.kill("SIGTERM");
        setTimeout(() => child.kill("SIGKILL"), 2000);
      }, timeoutMs);
    }
    child.stdout.on("data", (c: Buffer) => {
      stdout += c.toString("utf8");
    });
    child.stderr.on("data", (c: Buffer) => {
      stderr += c.toString("utf8");
    });
    child.on("error", (err) => {
      if (timer) {
        clearTimeout(timer);
      }
      reject(err);
    });
    child.on("close", (code) => {
      if (timer) {
        clearTimeout(timer);
      }
      resolve({ stdout, stderr, code, timedOut });
    });
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
