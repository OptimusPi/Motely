import { resolve } from "node:path";
import { defineConfig } from "vite";
import dts from "vite-plugin-dts";

const PEER_EXTERNALS = [
  "react",
  "react-dom",
  "react/jsx-runtime",
  "react/jsx-dev-runtime",
  "three",
  "@react-three/fiber",
  "@react-three/drei",
  "@react-spring/three",
  "react-icons",
  /^react-icons\//,
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
        r3f: resolve(__dirname, "src/r3f.ts"),
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
      },
    },
  },
});
