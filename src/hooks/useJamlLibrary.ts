"use client";

import { useCallback, useState } from "react";
import { Motely } from "motely-wasm";
import { ensureMotelyReady } from "../lib/motely/runtime.js";
import { IFileMounter, PermissionMode } from "motely-wasm/bootsharp/file-system";

type FileSystemPackage = typeof import("@rewaffle/bootsharp-file-system");

let fileSystemPackage: FileSystemPackage | null = null;
let fileSystemInitError: unknown = null;

try {
  fileSystemPackage = await import("@rewaffle/bootsharp-file-system");
  fileSystemPackage.init(IFileMounter);
} catch (error) {
  fileSystemInitError = error;
}

export type JamlLibraryStatus = "idle" | "unsupported" | "mounting" | "ready" | "error";

export interface UseJamlLibraryState {
  status: JamlLibraryStatus;
  rootId: string | null;
  files: string[];
  error: string | null;
  mount: () => Promise<void>;
  unmount: () => Promise<void>;
  loadFile: (uri: string) => Promise<string>;
  saveFile: (uri: string, content: string) => Promise<void>;
  refresh: () => void;
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}


export function useJamlLibrary(): UseJamlLibraryState {
  const isFileSystemReady = fileSystemPackage !== null;
  const [status, setStatus] = useState<JamlLibraryStatus>(() => isFileSystemReady ? "idle" : "unsupported");
  const [rootId, setRootId] = useState<string | null>(null);
  const [files, setFiles] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(() =>
    isFileSystemReady ? null : errorMessage(fileSystemInitError ?? "Bootsharp FileSystem package is not available."),
  );

  const refresh = useCallback(() => {
    if (!rootId) return;
    setFiles((prev) => [...prev]);
  }, [rootId]);

  const mount = useCallback(async () => {
    if (!isFileSystemReady) {
      setStatus("unsupported");
      setError(errorMessage(fileSystemInitError ?? "Bootsharp FileSystem package is not available."));
      return;
    }

    setStatus("mounting");
    setError(null);

    try {
      await ensureMotelyReady();
      const pickedRoot = await Motely.pickRoot({ mode: PermissionMode.ReadWrite, id: "jaml-library" });
      if (!pickedRoot) {
        setStatus("idle");
        return;
      }

      const mountedRoot = await Motely.mountRoot(pickedRoot, { mode: PermissionMode.ReadWrite });
      setRootId(mountedRoot);
      setFiles([]);
      setStatus("ready");
    } catch (err) {
      setStatus("error");
      setError(errorMessage(err));
    }
  }, [isFileSystemReady]);

  const unmount = useCallback(async () => {
    if (!rootId) return;
    await ensureMotelyReady();
    await Motely.unmountRoot(rootId);
    setRootId(null);
    setFiles([]);
    setStatus(isFileSystemReady ? "idle" : "unsupported");
  }, [isFileSystemReady, rootId]);

  const loadFile = useCallback(async (uri: string) => {
    if (!rootId) throw new Error("JAML library is not mounted.");
    await ensureMotelyReady();
    return await Motely.readTextFile(rootId, uri);
  }, [rootId]);

  const saveFile = useCallback(async (uri: string, content: string) => {
    if (!rootId) throw new Error("JAML library is not mounted.");
    await ensureMotelyReady();
    await Motely.writeTextFile(rootId, uri, content);
    setFiles((prev) => (prev.includes(uri) ? prev : [...prev, uri]).sort((a, b) => a.localeCompare(b)));
  }, [rootId]);

  return { status, rootId, files, error, mount, unmount, loadFile, saveFile, refresh };
}
