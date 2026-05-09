import type { RuntimeAPI } from "./modules";
/** Assigns the initialized .NET runtime to be used by Bootsharp. */
export declare function setRuntime(value: RuntimeAPI): void;
export declare function getHeap(): Uint8Array;
export declare function malloc(size: number): number;
export declare function free(ptr: number): void;
