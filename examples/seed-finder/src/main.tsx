import { createRoot } from "react-dom/client";
import bootsharp from "motely-wasm";
import { App } from "./App";

// Boot motely-wasm once before React mounts. By the time any component
// renders, the runtime is up — useSearch / useAnalyzer don't need to
// wait. The bin/ folder is mirrored into public/ by scripts/copy-motely-bin.mjs.
await bootsharp.boot("/motely-wasm/bin");

createRoot(document.getElementById("root")!).render(<App />);
