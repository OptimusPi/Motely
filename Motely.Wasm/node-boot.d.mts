export interface BootResources {
    wasm: ArrayBuffer;
    assemblies: Array<{ name: string; content: ArrayBuffer }>;
    icu: Array<{ name: string; content: ArrayBuffer }>;
    symbols: Array<{ name: string; content: ArrayBuffer }>;
    pdb: Array<{ name: string; content: ArrayBuffer }>;
}

/** @param binDir Absolute path to the package bin/ directory. */
export function loadBootResourcesFromDir(binDir: string): Promise<BootResources>;

export function resolvePackageBinDir(): string;
