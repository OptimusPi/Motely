import { resolve } from "node:path";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import dts from "vite-plugin-dts";

/**
 * Vite library build config for jaml-seed-lab
 *
 * Builds the src/ directory into dist/ for npm package exports.
 * Next.js app pages are NOT included in the library build.
 */

export default defineConfig({
  plugins: [
    react(),
    dts({
      insertTypesEntry: true,
      tsconfigPath: "./tsconfig.json",
      include: ["src/**/*.ts", "src/**/*.tsx", "lib/**/*.ts"],
    }),
  ],
  build: {
    lib: {
      entry: {
        index: resolve(__dirname, "src/index.ts"),
        "apps/ide": resolve(__dirname, "src/apps/ide/index.ts"),
        "apps/home": resolve(__dirname, "src/apps/home/index.ts"),
        "apps/finder": resolve(__dirname, "src/apps/finder/index.ts"),
        "apps/analyzer": resolve(__dirname, "src/apps/analyzer/index.ts"),
        mcp: resolve(__dirname, "src/mcp/index.ts"),
        codemirror: resolve(__dirname, "src/codemirror/index.ts"),
        catalog: resolve(__dirname, "lib/catalog.ts"),
        registry: resolve(__dirname, "lib/registry.tsx"),
        "spec-builder": resolve(__dirname, "lib/spec-builder.ts"),
      },
      formats: ["es"],
    },
    rollupOptions: {
      external: [
        "react",
        "react-dom",
        "react/jsx-runtime",
        "next",
        "next/link",
        "next/head",
        "jaml-ui",
        "jaml-ui/ui",
        "motely-wasm",
        "@json-render/core",
        "@json-render/react",
        "@json-render/shadcn",
        "jaml-lang",
        "zod",
        "lucide-react",
        "react-icons",
        "@codemirror/state",
        "@codemirror/view",
        "@modelcontextprotocol/sdk",
        "@modelcontextprotocol/ext-apps",
      ],
      output: {
        preserveModules: false,
        chunkFileNames: "chunks/[name].js",
      },
    },
    outDir: "dist",
    emptyOutDir: true,
    sourcemap: true,
  },
});
