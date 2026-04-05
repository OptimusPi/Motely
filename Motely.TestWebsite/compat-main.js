import bootsharp, { MotelyProgram } from "motely-wasm-compat";

const pre = document.getElementById("out");

try {
  await bootsharp.boot();
  const ver = MotelyProgram.getVersion();
  pre.textContent = `OK motely-wasm-compat\nMotelyProgram.getVersion() = ${ver}`;
} catch (e) {
  pre.textContent = `Error: ${e?.message ?? e}`;
  console.error(e);
}
