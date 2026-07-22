import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  resolve: {
    // jaml-ui is a file: symlink into its own repo; dedupe keeps one React and
    // one motely-wasm in the bundle so hooks and enum identity stay happy.
    dedupe: ["react", "react-dom", "motely-wasm"],
  },
});
