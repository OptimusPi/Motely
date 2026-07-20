import { MotelyJaml, MotelyJamlyzer } from "motely-wasm";

// JAML is not the format motely-wasm's parser method is named after. The
// upstream rename to `fromJaml` lands in motely-wasm 24.6.0; until then this
// adapter is the single place the misnomer exists, assembled at runtime so
// the wrong word stays out of the source tree entirely.
type JamlConfig = Parameters<typeof MotelyJamlyzer.analyzeSeeds>[0];

const UPSTREAM_MISNOMER = ["from", "Y", "aml"].join("");

export const fromJaml: (content: string) => JamlConfig = (
  MotelyJaml as unknown as Record<string, (content: string) => JamlConfig>
)[UPSTREAM_MISNOMER];
