import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // motely-wasm uses dynamic import() with @vite-ignore — Webpack never sees
  // the _framework/ assets. They load at runtime from the npm package path or
  // a CDN baseUrl you pass to loadMotely(). No CopyPlugin needed.

  async headers() {
    return [
      {
        // SharedArrayBuffer requires cross-origin isolation on ALL routes
        source: "/(.*)",
        headers: [
          { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
          { key: "Cross-Origin-Embedder-Policy", value: "require-corp" },
        ],
      },
    ];
  },

  // If you self-host _framework/ as static files instead of loading from
  // node_modules, put them in public/_framework/ and you're done.
  // No webpack config needed either way.
};

export default nextConfig;
