"""Static server with correct MIME types for ESM (.mjs / .wasm)."""
import http.server
import socketserver
import sys

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 3141

class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {
        **http.server.SimpleHTTPRequestHandler.extensions_map,
        ".mjs": "application/javascript",
        ".js": "application/javascript",
        ".wasm": "application/wasm",
        ".json": "application/json",
        ".css": "text/css",
        ".html": "text/html",
        ".svg": "image/svg+xml",
        ".png": "image/png",
        ".woff2": "font/woff2",
        "": "application/octet-stream",
    }

with socketserver.ThreadingTCPServer(("", PORT), Handler) as httpd:
    print(f"serving on http://localhost:{PORT}/")
    httpd.serve_forever()
