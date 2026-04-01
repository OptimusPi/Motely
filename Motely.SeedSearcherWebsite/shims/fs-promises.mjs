/** Stub so Bootsharp/dotnet.g dynamic `import('fs/promises')` resolves in the browser (never executed on WASM path). */
export async function readFile() {
  throw new Error("fs/promises is not available in the browser bundle");
}
