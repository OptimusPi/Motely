import { resolve } from "node:path";
import { defineConfig } from "vite";
import dts from "vite-plugin-dts";

// Entries whose source has a top-level "use client" directive. Vite's library
// build strips module-level directives when bundling, so Next.js's RSC
// compiler can't see these are client boundaries and tries to render them as
// Server Components — crashing on any hook use (e.g. "useRef is not a
// function"). Re-adding the banner only to these entries (not shared chunks
// like core.js/spriteMapper) is enough: Next only checks the directive on the
// module a consumer actually imports.
const CLIENT_ENTRIES = new Set(["index.js", "ui.js", "motely.js"]);

const PEER_EXTERNALS = [
  "react",
  "react-dom",
  "react/jsx-runtime",
  "react/jsx-dev-runtime",
  // motely-wasm: externalize so consumers control resolution. Next.js apps
  // get it via npm transitive resolution; the singlefile MCP iframe gets it
  // via an importmap pointing at unpkg (browser fetches once, caches across
  // tool invocations). Bundling here would balloon the iframe HTML.
  "motely-wasm",
  /^motely-wasm\//,
  "@rewaffle/bootsharp-file-system",
];

export default defineConfig({
  plugins: [
    dts({
      entryRoot: "src",
      include: ["src"],
      exclude: [
        "src/**/*.stories.tsx",
        "src/**/*.stories.ts",
        "src/**/*.test.tsx",
        "src/**/*.test.ts",
        // Storybook-only source: pulls in jaml-codemirror, which is a sibling
        // workspace package (not a jaml-ui dependency). Built from source by
        // Storybook directly; never part of the published package.
        "src/components/SeedFinderApp.tsx",
        "src/components/McpSeedFinderApp.tsx",
      ],
      tsconfigPath: "./tsconfig.json",
    }),
  ],
  build: {
    outDir: "dist",
    emptyOutDir: true,
    cssCodeSplit: false,
    sourcemap: true,
    lib: {
      entry: {
        index: resolve(__dirname, "src/index.ts"),
        ui: resolve(__dirname, "src/ui.ts"),
        core: resolve(__dirname, "src/core.ts"),
        motely: resolve(__dirname, "src/motely.ts"),
      },
      formats: ["es"],
    },
    rollupOptions: {
      external: PEER_EXTERNALS,
      output: {
        preserveModules: false,
        entryFileNames: "[name].js",
        chunkFileNames: "chunks/[name]-[hash].js",
        assetFileNames: (info) => {
          if (info.name?.endsWith(".css")) return "ui/jimbo.css";
          return "assets/[name]-[hash][extname]";
        },
        banner: (chunk) => (CLIENT_ENTRIES.has(chunk.fileName) ? '"use client";' : ""),
      },
    },
  },
});
