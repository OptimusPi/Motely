import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true,
  transpilePackages: ["jaml-ui"],
  async redirects() {
    return [
      { source: "/find", destination: "/finder", permanent: true },
      { source: "/analyze", destination: "/analyzer", permanent: true },
      { source: "/erratic", destination: "/analyzer", permanent: false },
      { source: "/mcp", destination: "/api/mcp", permanent: false },
    ];
  },
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
