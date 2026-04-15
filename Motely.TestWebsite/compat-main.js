import bootsharp, { MotelyWasm } from "motely-wasm";

const pre = document.getElementById("out");

try {
  await bootsharp.boot();
  const ver = MotelyWasm.getVersion();
  pre.textContent = `OK motely-wasm\nMotelyWasm.getVersion() = ${ver}`;
} catch (e) {
  pre.textContent = `Error: ${e?.message ?? e}`;
  console.error(e);
}
