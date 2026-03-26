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
};

export default nextConfig;
