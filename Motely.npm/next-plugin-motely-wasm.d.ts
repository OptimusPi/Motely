import type { NextConfig } from "next";

declare function withMotelyWasm(nextConfig?: NextConfig): NextConfig;
export = withMotelyWasm;
