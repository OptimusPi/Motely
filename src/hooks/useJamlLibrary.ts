"use client";

import { useCallback, useEffect, useState } from "react";
import { Program as Motely } from "motely-wasm/motely/wasm";
import { ensureMotelyReady } from "../lib/motely/runtime.js";
import { IFileMounter, PermissionMode } from "motely-wasm/bootsharp/file-system";

type FileSystemPackage = typeof import("@rewaffle/bootsharp-file-system");

let fileSystemPackage: FileSystemPackage | null = null;
let fileSystemInitError: unknown = null;
let fileSystemInitPromise: Promise<void> | null = null;

function initFileSystem(): Promise<void> {
  if (fileSystemInitPromise) return fileSystemInitPromise;
  fileSystemInitPromise = (async () => {
    try {
      fileSystemPackage = await import("@rewaffle/bootsharp-file-system");
      fileSystemPackage.init(IFileMounter);
    } catch (error) {
      fileSystemInitError = error;
    }
  })();
  return fileSystemInitPromise;
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
  const [status, setStatus] = useState<JamlLibraryStatus>("idle");
  const [rootId, setRootId] = useState<string | null>(null);
  const [files, setFiles] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    initFileSystem().then(() => {
      if (cancelled) return;
      if (fileSystemPackage === null) {
        setStatus("unsupported");
        setError(errorMessage(fileSystemInitError ?? "Bootsharp FileSystem package is not available."));
      }
    });
    return () => { cancelled = true; };
  }, []);

  const isFileSystemReady = fileSystemPackage !== null;

  const refresh = useCallback(() => {
    if (!rootId) return;
    setFiles((prev) => [...prev]);
  }, [rootId]);

  const mount = useCallback(async () => {
    await initFileSystem();
    if (fileSystemPackage === null) {
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
  }, []);

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
