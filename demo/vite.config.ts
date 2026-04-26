import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { resolve } from "node:path";

export default defineConfig({
  root: __dirname,
  plugins: [react()],
  resolve: {
    alias: {
      "jaml-ui/ui": resolve(__dirname, "../src/ui.ts"),
      "jaml-ui": resolve(__dirname, "../src/index.ts"),
    },
  },
  server: {
    port: 5174,
    open: true,
  },
});
