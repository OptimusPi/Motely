import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const isMcp = mode === "mcp";

  return {
    plugins: [react()],
    base: isMcp ? "./" : "/",
    server: { port: 5174 },
    build: {
      sourcemap: false,
      outDir: isMcp ? "dist" : "dist-web",
      emptyOutDir: true,
      rollupOptions: {
        input: isMcp ? "mcp-app.html" : "index.html",
      },
    },
  };
});
