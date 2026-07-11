import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import bootsharp, { Jimmolate } from "motely-wasm";
import { App } from "./App.js";

Jimmolate.filter = () => 1;
await bootsharp.boot();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>
);
