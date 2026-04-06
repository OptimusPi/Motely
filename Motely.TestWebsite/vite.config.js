import { defineConfig } from "vite";
import { resolve } from "node:path";

export default defineConfig({
  root: ".",
  base: "/MotelyJAML/",
  build: {
    target: "es2022",
    chunkSizeWarningLimit: 24000,
    rollupOptions: {
      input: {
        main: resolve(__dirname, "index.html"),
        compat: resolve(__dirname, "compat.html"),
      },
      onwarn(warning, warn) {
        // motely-wasm uses Node built-ins conditionally; Vite stubs them — safe to ignore.
        if (warning.message?.includes("has been externalized for browser compatibility")) return;
        warn(warning);
      },
    },
  },
});
