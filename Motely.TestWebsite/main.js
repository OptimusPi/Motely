import bootsharp, { MotelyProgram } from "motely-wasm";

const pre = document.getElementById("out");

try {
  await bootsharp.boot();
  const ver = MotelyProgram.getVersion();
  pre.textContent = `OK motely-wasm\nMotelyProgram.getVersion() = ${ver}`;
} catch (e) {
  pre.textContent = `Error: ${e?.message ?? e}`;
  console.error(e);
}
