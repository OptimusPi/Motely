import { createRoot } from "react-dom/client";
import { useState } from "react";
import bootsharp, { Jimmolate } from "motely-wasm";
import { SeedFinderApp } from "./SeedFinderApp";
import { STARTER_JAML } from "./constants";

Jimmolate.filter = () => 1;

function StandaloneApp() {
  const [jaml, setJaml] = useState(STARTER_JAML);
  return <SeedFinderApp jaml={jaml} onChange={setJaml} />;
}

await bootsharp.boot();
createRoot(document.getElementById("root")!).render(<StandaloneApp />);
