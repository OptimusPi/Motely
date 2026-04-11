import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { JamlSearchApp } from "./JamlSearchApp.js";

const el = document.getElementById("root");
if (el) {
  createRoot(el).render(
    <StrictMode>
      <JamlSearchApp />
    </StrictMode>,
  );
}
