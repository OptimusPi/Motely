import { createRoot } from "react-dom/client";
import bootsharp, { Jimmolate } from "motely-wasm";
import "jaml-ui/jimbo.css";
import "jaml-ui/fonts.css";
import { App } from "./App";
import { STARTER_JAML } from "./constants";

Jimmolate.filter = () => 1;
await bootsharp.boot();
createRoot(document.getElementById("root")!).render(<App initialJaml={STARTER_JAML} />);
