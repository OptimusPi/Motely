import bootsharp, { Motely } from "motely-wasm";
import { IFileMounter } from "motely-wasm/bootsharp/file-system";

type FileSystemPackage = typeof import("@rewaffle/bootsharp-file-system");

let fileSystemPackage: FileSystemPackage | null = null;
let fileSystemInitError: unknown = null;

try {
  fileSystemPackage = await import("@rewaffle/bootsharp-file-system");
  fileSystemPackage.init(IFileMounter);
} catch (error) {
  // The FileSystem package is private/registry-scoped. Search and analysis
  // still work without it; library mount APIs will fail until it is installed.
  fileSystemInitError = error;
}

const BOOT_ROOT_CANDIDATES = ["/motely-wasm/bin", "/node_modules/motely-wasm/bin", "/bin"];

let booted = false;
let bootError: unknown = null;

if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
  for (const root of BOOT_ROOT_CANDIDATES) {
    try {
      await bootsharp.boot(root);
      booted = true;
      break;
    } catch (error) {
      bootError = error;
    }
  }
} else {
  booted = true;
}

if (!booted) {
  console.error("Failed to boot Motely WASM runtime:", bootError);
}

export type MotelyRuntimeStatus = "idle" | "booting" | "ready" | "error";

export interface MotelyRuntimeSnapshot {
  status: MotelyRuntimeStatus;
  error: unknown;
  isFileSystemReady: boolean;
  fileSystemError: unknown;
}

export function ensureMotelyReady(): Promise<void> {
  return Promise.resolve();
}

export function getMotelyRuntimeSnapshot(): MotelyRuntimeSnapshot {
  return {
    status: booted ? "ready" : "error",
    error: bootError,
    isFileSystemReady: fileSystemPackage !== null,
    fileSystemError: fileSystemInitError,
  };
}

export function subscribeMotelyRuntime(listener: () => void): () => void {
  // Runtime state is static after module load, so listeners don't need to fire
  return () => {};
}

export const MotelyFileSystem = fileSystemPackage;
export const motelyFileSystemInitError = fileSystemInitError;
export const isMotelyFileSystemReady = fileSystemPackage !== null;

export { Motely };
