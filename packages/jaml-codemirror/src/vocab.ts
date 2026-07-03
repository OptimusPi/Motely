import { MotelyJaml } from "motely-wasm";

/**
 * Engine-served vocabulary, one list per listItems kind.
 * The host app owns bootsharp.boot(); until it completes, listItems throws
 * and getVocabulary() returns null — completions fall back to key-only and
 * retry on the next keystroke.
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

let cached: Vocabulary | null = null;

export function getVocabulary(): Vocabulary | null {
  if (cached) return cached;
  try {
    const vocab: Record<string, readonly string[]> = {};
    for (const kind of KINDS) {
      vocab[kind] = MotelyJaml.listItems(kind, "");
    }
    cached = vocab;
    return cached;
  } catch {
    return null; // engine not booted yet — retry next call
  }
}

/** Maps a JAML clause key to the engine vocabulary kind its value draws from. */
export function kindForKey(key: string): string | null {
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
