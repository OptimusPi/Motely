/** Minimal `url` shim for browser (dotnet.g.js). */
export const URL = globalThis.URL;
export function fileURLToPath(href) {
  if (typeof href === "string" && href.startsWith("file:")) {
    try {
      return new URL(href).pathname;
    } catch {
      /* fall through */
    }
  }
  throw new Error("fileURLToPath: not supported in browser");
}
export function pathToFileURL(p) {
  return new URL(`file://${String(p).replace(/\\/g, "/")}`);
}
