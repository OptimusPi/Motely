import { defineConfig } from 'vite';
import path from 'path';
import fs from 'fs';

// Serve motely-wasm dist at /motely-wasm (this site's Vite setup, not in the package)
function serveMotelyWasm() {
  const servePath = '/motely-wasm';
  let distDir: string;
  const MIME: Record<string, string> = {
    '.js': 'application/javascript',
    '.mjs': 'application/javascript',
    '.wasm': 'application/wasm',
    '.json': 'application/json',
  };
  function cpRecursive(src: string, dest: string) {
    if (!fs.existsSync(src)) return;
    const stat = fs.statSync(src);
    if (stat.isDirectory()) {
      if (!fs.existsSync(dest)) fs.mkdirSync(dest, { recursive: true });
      fs.readdirSync(src).forEach((entry) => {
        cpRecursive(path.join(src, entry), path.join(dest, entry));
      });
      return;
    }
    fs.copyFileSync(src, dest);
  }
  return {
    name: 'serve-motely-wasm',
    configResolved(config: { root: string }) {
      distDir = path.resolve(config.root, 'node_modules', 'motely-wasm', 'dist');
    },
    configureServer(server: { middlewares: { use: (p: string, fn: (req: any, res: any, next: () => void) => void) => void } }) {
      if (!fs.existsSync(distDir)) return;
      server.middlewares.use(servePath, (req: { url?: string }, res: { setHeader: (k: string, v: string) => void; end?: () => void }, next: () => void) => {
        const sub = (req.url || '/').replace(/^\//, '').replace(/\?.*$/, '') || 'index.html';
        const file = path.join(distDir, sub);
        if (!fs.existsSync(file) || !fs.statSync(file).isFile()) return next();
        res.setHeader('Content-Type', MIME[path.extname(file)] || 'application/octet-stream');
        fs.createReadStream(file).pipe(res as any);
      });
    },
    closeBundle() {
      if (!fs.existsSync(distDir)) return;
      const out = path.resolve(process.cwd(), 'dist', servePath.replace(/^\//, ''));
      if (!fs.existsSync(path.dirname(out))) return;
      fs.mkdirSync(path.dirname(out), { recursive: true });
      cpRecursive(distDir, out);
    },
  };
}

export default defineConfig({
  plugins: [serveMotelyWasm()],
  server: {
    headers: {
      'Cross-Origin-Embedder-Policy': 'credentialless',
      'Cross-Origin-Opener-Policy': 'same-origin',
    },
  },
});
