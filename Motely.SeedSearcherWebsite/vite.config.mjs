import { defineConfig } from "vite";
import { resolve } from "node:path";

export default defineConfig({
  build: {
    rollupOptions: {
      input: {
        main: resolve(process.cwd(), "index.html"),
        coep: resolve(process.cwd(), "coep/index.html"),
      },
    },
  },
});
