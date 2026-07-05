import { defineConfig } from "vite";

// Self-contained ESM bundle: every CodeMirror dependency rides inside so an
// importmap consumer maps only "react", "react/jsx-runtime", and
// "motely-wasm". Those two stay external on purpose — the host app owns the
// single React and the single booted engine instance.
export default defineConfig({
  build: {
    lib: {
      entry: "src/index.ts",
      formats: ["es"],
      fileName: () => "index.js",
    },
    outDir: "dist",
    emptyOutDir: false,
    sourcemap: true,
    rollupOptions: {
      external: ["react", "react/jsx-runtime", "react-dom", "motely-wasm"],
    },
  },
});
