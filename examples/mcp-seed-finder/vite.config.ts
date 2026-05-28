import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { viteSingleFile } from "vite-plugin-singlefile";

const isDevelopment = process.env.NODE_ENV === "development";

export default defineConfig(({ mode }) => {
  const input = process.env.VITE_INPUT ?? (mode === "mcp" ? "mcp-app.html" : "index.html");
  const isMcp = mode === "mcp";

  return {
    plugins: [react(), isMcp ? viteSingleFile() : null].filter(Boolean),
    server: { port: 5174 },
    build: {
      sourcemap: isDevelopment ? "inline" : undefined,
      cssMinify: !isDevelopment,
      minify: !isDevelopment,
      rollupOptions: {
        input,
        external: [
          "motely-wasm",
          /^motely-wasm\//,
        ],
      },
      outDir: isMcp ? "dist" : "dist-web",
      emptyOutDir: true,
    },
  };
});
