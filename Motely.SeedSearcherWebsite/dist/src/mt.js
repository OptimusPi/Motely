import dotnet, { Motely } from "motely-wasm-mt";
import { bootAndWire } from "./shared.js";

function mtWasmBootRoot() {
  const u = new URL("../motely-wasm-mt/bin/", import.meta.url).href;
  return u.replace(/\/$/, "");
}

await bootAndWire(dotnet, Motely, "coep-sab", { bootRoot: mtWasmBootRoot() });
