import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  staticPageGenerationTimeout: 1000,
  turbopack: {
    resolveAlias: {
      fs: { browser: "./lib/stubs/empty.ts" },
      "fs/promises": { browser: "./lib/stubs/empty.ts" },
      url: { browser: "./lib/stubs/empty.ts" },
    },
  },
};

export default nextConfig;
