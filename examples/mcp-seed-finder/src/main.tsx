import { createRoot } from "react-dom/client";
import { useState } from "react";
import bootsharp from "motely-wasm";
import { bindJimmolateBridge } from "jaml-codemirror";
import { SeedFinderApp } from "./SeedFinderApp";
import { STARTER_JAML } from "./constants";

bindJimmolateBridge();

function StandaloneApp() {
  const [jaml, setJaml] = useState(STARTER_JAML);
  return <SeedFinderApp jaml={jaml} onChange={setJaml} />;
}

await bootsharp.boot();
createRoot(document.getElementById("root")!).render(<StandaloneApp />);
