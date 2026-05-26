export function normalizeJamlSeed(seed: string): string {
  return seed
    .toUpperCase()
    .replace(/0/g, "O")
    .replace(/[^A-Z0-9]/g, "")
    .slice(0, 8);
}
