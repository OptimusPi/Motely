"use client";

import { useCallback, useRef, useState } from "react";
import { isFileSystemReady, getFileSystemError } from "../lib/motely/runtime.js";

// motely-wasm@23 removed the engine-side filesystem ops, so the JAML library is
// backed directly by the browser File System Access API. JAML files are plain
// text, so mounting a folder, listing its .jaml/.yaml files, and reading/writing
// them needs no engine round-trip. Where the API is unavailable (non-Chromium or
// insecure context) status is "unsupported" — reported honestly via
// isFileSystemReady() in runtime.ts, not a faked mount.

// Minimal typings for the slice of the File System Access API we use (keeps us
// off lib.dom shipping these and clear of the repo's no-explicit-any rule).
interface FsWritable {
  write(data: string): Promise<void>;
  close(): Promise<void>;
}
interface FsFileHandle {
  kind: "file";
  getFile(): Promise<File>;
  createWritable(): Promise<FsWritable>;
}
interface FsDirectoryHandle {
  kind: "directory";
  name: string;
  entries(): AsyncIterableIterator<[string, { kind: string }]>;
  getFileHandle(name: string, options?: { create?: boolean }): Promise<FsFileHandle>;
}
type DirectoryPicker = (options?: { id?: string; mode?: "read" | "readwrite" }) => Promise<FsDirectoryHandle>;

const JAML_FILE = /\.(jaml|ya?ml)$/i;

function directoryPicker(): DirectoryPicker | null {
  if (!isFileSystemReady()) return null;
  return (window as unknown as { showDirectoryPicker: DirectoryPicker }).showDirectoryPicker;
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
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

async function listJamlFiles(dir: FsDirectoryHandle): Promise<string[]> {
  const names: string[] = [];
  for await (const [name, handle] of dir.entries()) {
    if (handle.kind === "file" && JAML_FILE.test(name)) names.push(name);
  }
  return names.sort((a, b) => a.localeCompare(b));
}

export function useJamlLibrary(): UseJamlLibraryState {
  const [status, setStatus] = useState<JamlLibraryStatus>(() => (isFileSystemReady() ? "idle" : "unsupported"));
  const [rootId, setRootId] = useState<string | null>(null);
  const [files, setFiles] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const dirRef = useRef<FsDirectoryHandle | null>(null);

  const refresh = useCallback(() => {
    const dir = dirRef.current;
    if (!dir) return;
    void listJamlFiles(dir).then(setFiles).catch((e) => setError(errorMessage(e)));
  }, []);

  const mount = useCallback(async () => {
    const pick = directoryPicker();
    if (!pick) {
      setStatus("unsupported");
      setError(errorMessage(getFileSystemError()));
      return;
    }
    setStatus("mounting");
    setError(null);
    try {
      const dir = await pick({ id: "jaml-library", mode: "readwrite" });
      dirRef.current = dir;
      setRootId(dir.name);
      setFiles(await listJamlFiles(dir));
      setStatus("ready");
    } catch (err) {
      // The user dismissing the directory picker raises AbortError — not a failure.
      if (err instanceof DOMException && err.name === "AbortError") {
        setStatus(dirRef.current ? "ready" : "idle");
        return;
      }
      setStatus("error");
      setError(errorMessage(err));
    }
  }, []);

  const unmount = useCallback(async () => {
    dirRef.current = null;
    setRootId(null);
    setFiles([]);
    setStatus(isFileSystemReady() ? "idle" : "unsupported");
  }, []);

  const loadFile = useCallback(async (uri: string): Promise<string> => {
    const dir = dirRef.current;
    if (!dir) throw new Error("JAML library is not mounted.");
    const handle = await dir.getFileHandle(uri);
    const file = await handle.getFile();
    return file.text();
  }, []);

  const saveFile = useCallback(async (uri: string, content: string): Promise<void> => {
    const dir = dirRef.current;
    if (!dir) throw new Error("JAML library is not mounted.");
    const handle = await dir.getFileHandle(uri, { create: true });
    const writable = await handle.createWritable();
    await writable.write(content);
    await writable.close();
    setFiles((prev) => (prev.includes(uri) ? prev : [...prev, uri].sort((a, b) => a.localeCompare(b))));
  }, []);

  return { status, rootId, files, error, mount, unmount, loadFile, saveFile, refresh };
}
