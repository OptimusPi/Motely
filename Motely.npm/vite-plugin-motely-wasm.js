import path from "path";
import fs from "fs";
import { createRequire } from "module";

const require = createRequire(import.meta.url);

const COOP_COEP = {
  "Cross-Origin-Opener-Policy": "same-origin",
  "Cross-Origin-Embedder-Policy": "require-corp",
};

function findFrameworkDir(subdir) {
  const cwd = process.cwd();
  const candidate = path.join(cwd, "node_modules", "motely-wasm", subdir);
  if (fs.existsSync(candidate)) return candidate;
  try {
    const pkg = require.resolve("motely-wasm/package.json", { paths: [cwd] });
    const dir = path.join(path.dirname(pkg), subdir);
    if (fs.existsSync(dir)) return dir;
    return null;
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
 * Vite plugin: serves _framework in dev, copies to dist on build, sets COOP/COEP.
 * @returns {import('vite').Plugin}
 */
function findPackageRoot() {
  const cwd = process.cwd();
  const candidate = path.join(cwd, "node_modules", "motely-wasm");
  if (fs.existsSync(candidate)) return candidate;
  try {
    const pkg = require.resolve("motely-wasm/package.json", { paths: [cwd] });
    return path.dirname(pkg);
  } catch {
    return null;
  }
}

function motelyWasm() {
  let frameworkDirs = [];
  let outDir = "dist";
  let packageRoot = null;

  return {
    name: "motely-wasm",
    config(config) {
      packageRoot = findPackageRoot();
      frameworkDirs = [
        { subdir: "_framework", dir: findFrameworkDir("_framework") },
      ].filter(x => x.dir);
      const headers = { ...COOP_COEP };
      const existing = config.server?.headers;
      if (existing && typeof existing === "object") Object.assign(headers, existing);
      return {
        server: {
          headers,
        },
      };
    },
    configResolved(config) {
      outDir = config.build?.outDir ?? "dist";
    },
    configureServer(server) {
      if (!frameworkDirs.length) return;
      const types = {
        ".js": "text/javascript",
        ".mjs": "text/javascript",
        ".wasm": "application/wasm",
        ".json": "application/json",
        ".dat": "application/octet-stream",
      };
      for (const { subdir, dir } of frameworkDirs) {
        if (!dir || !fs.existsSync(dir)) continue;
        server.middlewares.use(`/${subdir}`, (req, res, next) => {
          const p = path.resolve(dir, (req.url === "/" ? "" : req.url).split("?")[0].replace(/^\//, ""));
          if (!p.startsWith(path.resolve(dir))) return next();
          try {
            const stat = fs.statSync(p);
            if (stat.isFile()) {
              res.setHeader("Cross-Origin-Opener-Policy", "same-origin");
              res.setHeader("Cross-Origin-Embedder-Policy", "require-corp");
              const ext = path.extname(p);
              if (types[ext]) res.setHeader("Content-Type", types[ext]);
              const stream = fs.createReadStream(p);
              stream.pipe(res);
              return;
            }
          } catch (_) { }
          next();
        });
      }
    },
    closeBundle() {
      const absOut = path.isAbsolute(outDir) ? outDir : path.resolve(process.cwd(), outDir);
      if (!fs.existsSync(absOut)) return;
      for (const { subdir, dir } of frameworkDirs) {
        if (!dir || !fs.existsSync(dir)) continue;
        const dest = path.join(absOut, subdir);
        copyDirRecursive(dir, dest);
      }
    },
  };
}

export default motelyWasm;
