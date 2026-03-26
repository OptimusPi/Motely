import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  transpilePackages: ["motely"],
  experimental: {
    externalDir: true,
  },
  turbopack: {
    resolveAlias: {
      // Relative to this app dir — Turbopack on Windows rejects absolute file:// imports here.
      "motely/browser": "../Motely/dist/wasm/index.mjs",
    },
  },
  // Serve static files from wwwroot
  staticPageGenerationTimeout: 1000,
  // Add rewrites for searcher.html
  async rewrites() {
    return [
      {
        source: '/searcher',
        destination: '/searcher.html',
      },
    ];
  },
};

export default nextConfig;
