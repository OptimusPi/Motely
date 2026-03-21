// motely-wasm — Bootsharp-powered .NET WASM runtime for Balatro seed search.
// Usage: import { boot, MotelyWasm } from "motely-wasm"

import bootsharp, { MotelyWasm, Event } from "./bootsharp/index.mjs";

let bootPromise = null;

/**
 * Boot the .NET WASM runtime. Call once; subsequent calls return the same promise.
 * Resolves the bootsharp sideload root automatically from this package's location.
 */
export function boot() {
    if (!bootPromise) {
        const root = new URL("./bootsharp", import.meta.url).href;
        bootPromise = bootsharp.boot({ root });
    }
    return bootPromise;
}

export { MotelyWasm, Event };
