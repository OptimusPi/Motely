import { MotelyJaml } from "motely-wasm";

/**
 * Engine-served vocabulary, one list per listItems kind.
 * Loaded once after bootsharp.boot(); names come straight from the
 * engine enums so nothing hand-maintained can drift.
 */
export type Vocabulary = Readonly<Record<string, readonly string[]>>;

const KINDS = [
  "joker",
  "voucher",
  "tag",
  "boss",
  "tarot",
  "planet",
  "spectral",
  "edition",
  "deck",
  "stake",
] as const;

/** Call after bootsharp.boot() — listItems needs the runtime up. */
export function loadVocabulary(): Vocabulary {
  const vocab: Record<string, readonly string[]> = {};
  for (const kind of KINDS) {
    vocab[kind] = MotelyJaml.listItems(kind, "");
  }
  // "Any" is a real JAML wildcard, but narrower than jaml-lang's own
  // completions.js assumes: MotelyJaml.fromYaml only accepts it for
  // joker/legendaryJoker (both map to "joker" below), not boss/voucher/
  // tag/deck — verified empirically against motely-wasm@23.3.0, not
  // copied from jaml-lang's unconditional (and partly wrong) unshift.
  vocab.joker = ["Any", ...vocab.joker];
  return vocab;
}

/** Maps a JAML clause key to the engine vocabulary kind its value draws from. */
export function kindForKey(key: unknown): string | null {
  switch (key) {
    case "joker":
    case "jokers":
    case "commonJoker":
    case "commonJokers":
    case "uncommonJoker":
    case "uncommonJokers":
    case "rareJoker":
    case "rareJokers":
    case "legendaryJoker":
    case "legendaryJokers":
      return "joker";
    case "voucher":
      return "voucher";
    case "tarotCard":
      return "tarot";
    case "planetCard":
      return "planet";
    case "spectralCard":
      return "spectral";
    case "boss":
      return "boss";
    case "tag":
    case "smallBlindTag":
    case "bigBlindTag":
      return "tag";
    case "edition":
      return "edition";
    case "deck":
      return "deck";
    case "stake":
      return "stake";
    default:
      return null;
  }
}
