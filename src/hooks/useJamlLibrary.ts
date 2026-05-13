"use client";

import { useCallback, useState } from "react";
import { Motely, isMotelyFileSystemReady, motelyFileSystemInitError } from "../motelyBoot.js";

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
  const [status, setStatus] = useState<JamlLibraryStatus>(() => isMotelyFileSystemReady ? "idle" : "unsupported");
  const [rootId, setRootId] = useState<string | null>(null);
  const [files, setFiles] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(() =>
    isMotelyFileSystemReady ? null : errorMessage(motelyFileSystemInitError ?? "Bootsharp FileSystem package is not available."),
  );

  const refresh = useCallback(() => {
    if (!rootId) return;
    setFiles(Array.from(Motely.MotelyWasm.getJamlLibraryFiles(rootId)));
  }, [rootId]);

  const mount = useCallback(async () => {
    if (!isMotelyFileSystemReady) {
      setStatus("unsupported");
      setError(errorMessage(motelyFileSystemInitError ?? "Bootsharp FileSystem package is not available."));
      return;
    }

    setStatus("mounting");
    setError(null);

    try {
      const pickedRoot = await Motely.MotelyWasm.mountJamlLibrary();
      if (!pickedRoot) {
        setStatus("idle");
        return;
      }

      setRootId(pickedRoot);
      setFiles(Array.from(Motely.MotelyWasm.getJamlLibraryFiles(pickedRoot)));
      setStatus("ready");
    } catch (err) {
      setStatus("error");
      setError(errorMessage(err));
    }
  }, []);

  const unmount = useCallback(async () => {
    if (!rootId) return;
    await Motely.MotelyWasm.unmountJamlLibrary(rootId);
    setRootId(null);
    setFiles([]);
    setStatus(isMotelyFileSystemReady ? "idle" : "unsupported");
  }, [rootId]);

  const loadFile = useCallback(async (uri: string) => {
    if (!rootId) throw new Error("JAML library is not mounted.");
    return await Motely.MotelyWasm.loadLibraryFile(rootId, uri);
  }, [rootId]);

  const saveFile = useCallback(async (uri: string, content: string) => {
    if (!rootId) throw new Error("JAML library is not mounted.");
    await Motely.MotelyWasm.saveLibraryFile(rootId, uri, content);
    setFiles(Array.from(Motely.MotelyWasm.getJamlLibraryFiles(rootId)));
  }, [rootId]);

  return { status, rootId, files, error, mount, unmount, loadFile, saveFile, refresh };
}
