import { createRoot } from "react-dom/client";
import { useState } from "react";
import bootsharp from "motely-wasm";
import { SeedFinderApp } from "./SeedFinderApp";
import { STARTER_JAML } from "./constants";

function StandaloneApp() {
  const [jaml, setJaml] = useState(STARTER_JAML);
  return <SeedFinderApp jaml={jaml} onChange={setJaml} />;
}

await bootsharp.boot("/motely-wasm/bin");
createRoot(document.getElementById("root")!).render(<StandaloneApp />);
