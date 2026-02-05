"use strict";

const path = require("path");
const fs = require("fs");

const COOP_COEP = [
  { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
  { key: "Cross-Origin-Embedder-Policy", value: "require-corp" },
];

function findFrameworkDir() {
  const cwd = process.cwd();
  const candidate = path.join(cwd, "node_modules", "motely-wasm", "_framework");
  if (fs.existsSync(candidate)) return candidate;
  try {
    const pkg = require.resolve("motely-wasm/package.json", { paths: [cwd] });
    return path.join(path.dirname(pkg), "_framework");
  } catch {
    return null;
  }
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
 * One-time setup: wrap config with withMotelyWasm(nextConfig).
 * @param {import('next').NextConfig} nextConfig
 * @returns {import('next').NextConfig}
 */
function withMotelyWasm(nextConfig = {}) {
  const frameworkDir = findFrameworkDir();
  const publicDir = path.join(process.cwd(), "public");
  if (frameworkDir && fs.existsSync(frameworkDir) && fs.existsSync(publicDir)) {
    const dest = path.join(publicDir, "_framework");
    copyDirRecursive(frameworkDir, dest);
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

module.exports = withMotelyWasm;
withMotelyWasm.default = withMotelyWasm;
