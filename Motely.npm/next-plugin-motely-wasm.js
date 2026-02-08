import path from "path";
import fs from "fs";
import { createRequire } from "module";

const require = createRequire(import.meta.url);

const COOP_COEP = [
  { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
  { key: "Cross-Origin-Embedder-Policy", value: "require-corp" },
];

function findFrameworkDir(subdir) {
  const cwd = process.cwd();
  const candidate = path.join(cwd, "node_modules", "motely-wasm", subdir);
  if (fs.existsSync(candidate)) return candidate;
  try {
    const pkg = require.resolve("motely-wasm/package.json", { paths: [cwd] });
    const dir = path.join(path.dirname(pkg), subdir);
    if (fs.existsSync(dir)) return dir;
  } catch {
    const fallbackDir = path.join(cwd, "..", "node_modules", "motely-wasm", subdir);
    if (fs.existsSync(fallbackDir)) return fallbackDir;
  }
  return null;
}

function copyDirRecursive(src, dest) {
  if (!fs.existsSync(dest)) fs.mkdirSync(dest, { recursive: true });
  for (const name of fs.readdirSync(src)) {
    const s = path.join(src, name);
    const d = path.join(dest, name);
    if (fs.statSync(s).isDirectory()) copyDirRecursive(s, d);
    else fs.copyFileSync(s, d);
  }
}

/**
 * Next.js plugin: copies _framework to public/_framework and sets COOP/COEP.
 * Usage: export default withMotelyWasm({ ...yourNextConfig });
 * @param {import('next').NextConfig} nextConfig
 * @returns {import('next').NextConfig}
 */
function withMotelyWasm(nextConfig = {}) {
  const frameworkDirs = [
    { subdir: "_framework", dir: findFrameworkDir("_framework") },
  ];
  const publicDir = path.join(process.cwd(), "public");

  if (!fs.existsSync(publicDir)) {
    fs.mkdirSync(publicDir, { recursive: true });
  }
  const logKey = "__motelyWasmNextPluginLogged";
  let copiedAny = false;
  for (const { subdir, dir } of frameworkDirs) {
    if (dir && fs.existsSync(dir)) {
      const dest = path.join(publicDir, subdir);
      copyDirRecursive(dir, dest);
      copiedAny = true;
    }
  }
  if (copiedAny && !globalThis[logKey]) {
    globalThis[logKey] = true;
    console.log("[motely-wasm] public/_framework ready");
  }
  if (!copiedAny) {
    console.warn("[motely-wasm] _framework not found. Run: npm install motely-wasm");
  }

  const existingHeaders = nextConfig.headers;
  const motelyHeaders = async () => {
    const list = [{ source: "/:path*", headers: COOP_COEP }];
    if (typeof existingHeaders === "function") {
      const existing = await existingHeaders();
      return [...existing, ...list];
    }
    if (Array.isArray(existingHeaders)) return [...existingHeaders, ...list];
    return list;
  };

  return {
    ...nextConfig,
    headers: motelyHeaders,
  };
}

export default withMotelyWasm;
