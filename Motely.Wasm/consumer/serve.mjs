// Static server for a consumer app: the page is served from here, and /motely/ maps straight
// into node_modules/motely-wasm/dist — the package as npm installed it, untouched.
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";

const here = fileURLToPath(new URL(".", import.meta.url));
const pkg = join(here, "node_modules", "motely-wasm", "dist");
const port = Number(process.env.PORT ?? 4180);

const types = {
  ".html": "text/html; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json",
  ".wasm": "application/wasm",
  ".dat": "application/octet-stream",
  ".css": "text/css; charset=utf-8",
};

createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  const path = url.pathname === "/" ? "/index.html" : url.pathname;

  // Anything under /motely/ is the installed package; everything else is this app.
  const [root, rel] = path.startsWith("/motely/")
    ? [pkg, path.slice("/motely/".length)]
    : [here, path.slice(1)];

  const file = normalize(join(root, rel));
  if (!file.startsWith(normalize(root))) {
    res.writeHead(403).end();
    return;
  }
  try {
    const body = await readFile(file);
    res.writeHead(200, { "content-type": types[extname(file)] ?? "application/octet-stream" });
    res.end(body);
  } catch {
    res.writeHead(404).end("not found");
  }
}).listen(port, () => console.log(`seed finder at http://127.0.0.1:${port}/`));
