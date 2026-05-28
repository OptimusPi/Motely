import { createRoot } from "react-dom/client";
import { useState } from "react";
import bootsharp from "motely-wasm";
import "jaml-ui/jimbo.css";
import { SeedFinderApp } from "./SeedFinderApp";
import { STARTER_JAML } from "./constants";

function StandaloneApp() {
  const [jaml, setJaml] = useState(STARTER_JAML);
  return <SeedFinderApp jaml={jaml} onChange={setJaml} />;
}

await bootsharp.boot("https://cdn.jsdelivr.net/npm/motely-wasm@19.0.2/bin");
createRoot(document.getElementById("root")!).render(<StandaloneApp />);
