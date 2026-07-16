import bootsharp from "motely-wasm";

let bootPromise: Promise<void> | null = null;

export async function ensureMotelyReady(): Promise<void> {
  if (bootsharp.getStatus() === bootsharp.BootStatus.Booted) return;
  if (!bootPromise) {
    bootPromise = bootsharp.boot().then(() => undefined);
  }
  await bootPromise;
}
