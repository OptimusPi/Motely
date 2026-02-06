import path from "path";
import fs from "fs";
import { createRequire } from "module";

const require = createRequire(import.meta.url);

const COOP_COEP = {
  "Cross-Origin-Opener-Policy": "same-origin",
  "Cross-Origin-Embedder-Policy": "require-corp",
};

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
 * Vite plugin: serves _framework in dev, copies to dist on build, sets COOP/COEP.
 * One-time setup: add to plugins and you're done.
 * @returns {import('vite').Plugin}
 */
function motelyWasm() {
  let frameworkDir = null;
  let outDir = "dist";

  return {
    name: "motely-wasm",
    config(config) {
      frameworkDir = findFrameworkDir();
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
      if (!frameworkDir || !fs.existsSync(frameworkDir)) return;
      const { fs: vfs } = server;
      server.middlewares.use("/_framework", (req, res, next) => {
        const p = path.join(frameworkDir, req.url === "/" ? "" : req.url).split("?")[0];
        if (!p.startsWith(frameworkDir)) return next();
        try {
          const stat = fs.statSync(p);
          if (stat.isFile()) {
            res.setHeader("Cross-Origin-Opener-Policy", "same-origin");
            res.setHeader("Cross-Origin-Embedder-Policy", "require-corp");
            const stream = fs.createReadStream(p);
            const ext = path.extname(p);
            const types = { ".js": "text/javascript", ".wasm": "application/wasm", ".json": "application/json" };
            if (types[ext]) res.setHeader("Content-Type", types[ext]);
            stream.pipe(res);
            return;
          }
        } catch (_) {}
        next();
      });
    },
    closeBundle() {
      if (!frameworkDir || !fs.existsSync(frameworkDir)) return;
      const absOut = path.isAbsolute(outDir) ? outDir : path.resolve(process.cwd(), outDir);
      if (!fs.existsSync(absOut)) return;
      const dest = path.join(absOut, "_framework");
      copyDirRecursive(frameworkDir, dest);
    },
  };
}

export default motelyWasm;
