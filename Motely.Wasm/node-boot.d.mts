/**
 * Node/Bun/Deno boot resources loader. Use before bootsharp.boot() in Node —
 * Node fetch() can't load file:// bin URLs, so binaries must be loaded via fs
 * and passed as a BootResources object.
 */

export interface BootResource {
    name: string;
    content: ArrayBuffer;
}

export interface BootResources {
    wasm: ArrayBuffer;
    assemblies: BootResource[];
    icu: BootResource[];
    symbols: BootResource[];
    pdb: BootResource[];
}

/**
 * Read the manifest-listed wasm + assemblies + icu + symbols + pdb from a directory
 * and bundle them into a BootResources object ready to pass to `bootsharp.boot(...)`.
 * @param binDir Absolute path to the package `bin/` directory.
 */
export function loadBootResourcesFromDir(binDir: string): Promise<BootResources>;

/**
 * Resolve the `bin/` directory next to this module's `dist/` parent.
 * Returns the absolute path.
 */
export function resolvePackageBinDir(): string;
