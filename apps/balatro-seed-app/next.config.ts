import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true,
  transpilePackages: ["jaml-ui"],
  async rewrites() {
    return [
      {
        source: "/mcp",
        destination: "/api/mcp",
      },
    ];
  },
};

export default nextConfig;
