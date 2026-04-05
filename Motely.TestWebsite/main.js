import bootsharp, { MotelyWasmHost } from "motely-wasm";

const pre = document.getElementById("out");

try {
  await bootsharp.boot();
  const ver = MotelyWasmHost.getVersion();
  pre.textContent = `OK motely-wasm\nMotelyWasmHost.getVersion() = ${ver}`;
} catch (e) {
  pre.textContent = `Error: ${e?.message ?? e}`;
  console.error(e);
}
