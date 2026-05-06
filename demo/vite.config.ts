import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  root: __dirname,
  plugins: [react()],
  resolve: {},
  server: {
    port: 3141,
    strictPort: true,
    host: true,
    open: true,
    cors: true,
    allowedHosts: ["motelyjaml-pi.8pi.me", "localhost"],
  },
});
