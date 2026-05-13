import bootsharp, { Bootsharp, Motely } from "motely-wasm";

type FileSystemPackage = typeof import("@rewaffle/bootsharp-file-system");

let fileSystemPackage: FileSystemPackage | null = null;
let fileSystemInitError: unknown = null;

try {
  fileSystemPackage = await import("@rewaffle/bootsharp-file-system");
  fileSystemPackage.init(Bootsharp.FileSystem.FileMounter);
} catch (error) {
  // The FileSystem package is private/registry-scoped. Search and analysis
  // still work without it; library mount APIs will fail until it is installed.
  fileSystemInitError = error;
}

await bootsharp.boot();

export const MotelyFileSystem = fileSystemPackage;
export const motelyFileSystemInitError = fileSystemInitError;
export const isMotelyFileSystemReady = fileSystemPackage !== null;

export { Motely };
