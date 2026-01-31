// Serve AppBundle with COOP/COEP so WASM can use SharedArrayBuffer (multi-threading).
// Usage: node scripts/serve-with-headers.cjs [port] [dir]
// Default: port 3333, dir = current dir (run from AppBundle or pass path).

const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = parseInt(process.argv[2], 10) || 3333;
const ROOT = process.argv[3] ? path.resolve(process.argv[3]) : process.cwd();

const MIME = {
  '.html': 'text/html',
  '.js': 'application/javascript',
  '.mjs': 'application/javascript',
  '.json': 'application/json',
  '.wasm': 'application/wasm',
  '.dll': 'application/octet-stream',
  '.dat': 'application/octet-stream',
  '.symbols': 'text/plain',
  '.map': 'application/json',
  '.rsp': 'text/plain',
  '.c': 'text/plain',
  '.h': 'text/plain',
};

const HEADERS = {
  'Cross-Origin-Opener-Policy': 'same-origin',
  'Cross-Origin-Embedder-Policy': 'require-corp',
};

const server = http.createServer((req, res) => {
  let url = (req.url || '/').split('?')[0];
  if (url === '/') url = '/index.html';
  const safe = path.normalize(url.replace(/^\//, '')).replace(/^(\.\.(\/|\\|$))+/, '');
  const filePath = path.join(ROOT, safe);
  if (!filePath.startsWith(ROOT)) {
    res.writeHead(403);
    res.end();
    return;
  }
  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(err.code === 'ENOENT' ? 404 : 500);
      res.end();
      return;
    }
    const ext = path.extname(filePath);
    const contentType = MIME[ext] || 'application/octet-stream';
    res.writeHead(200, { 'Content-Type': contentType, ...HEADERS });
    res.end(data);
  });
});

server.listen(PORT, () => {
  console.log('Serving with COOP/COEP at http://localhost:' + PORT);
  console.log('Root: ' + ROOT);
});
