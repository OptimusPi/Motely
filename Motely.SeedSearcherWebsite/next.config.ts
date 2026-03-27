import type { NextConfig } from "next";

const nextConfig: NextConfig = {
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
