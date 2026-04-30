import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  root: __dirname,
  plugins: [react()],
  resolve: {},
  server: {
    port: 3141,
    host: true,
    open: true,
    cors: true,
    allowedHosts: ["motelyjaml-pi.8pi.me", "localhost"],
  },
});
