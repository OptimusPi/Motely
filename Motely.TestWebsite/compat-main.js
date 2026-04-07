import bootsharp, { MotelyJamlSearchBuilder } from "motely-wasm-compat";

const pre = document.getElementById("out");

try {
  await bootsharp.boot();
  const ver = MotelyJamlSearchBuilder.getVersion();
  pre.textContent = `OK motely-wasm-compat\nMotelyJamlSearchBuilder.getVersion() = ${ver}`;
} catch (e) {
  pre.textContent = `Error: ${e?.message ?? e}`;
  console.error(e);
}
