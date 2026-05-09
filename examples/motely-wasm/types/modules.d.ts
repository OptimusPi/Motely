import type { ModuleAPI, MonoConfig } from "./dotnet.g.d.ts";
export type { Asset } from "./dotnet.g.d.ts";
export type * from "./dotnet.g.d.ts";
export type RuntimeConfig = MonoConfig;
export type RuntimeResources = NonNullable<RuntimeConfig["resources"]>;
export type WasmAsset = NonNullable<RuntimeResources["wasmNative"]>[number];
export type ModuleAsset = NonNullable<RuntimeResources["jsModuleNative"]>[number];
export type AssemblyAsset = NonNullable<RuntimeResources["assembly"]>[number];
export type IcuAsset = NonNullable<RuntimeResources["icu"]>[number];
export type PdbAsset = NonNullable<RuntimeResources["pdb"]>[number];
export type SymbolsAsset = NonNullable<RuntimeResources["wasmSymbols"]>[number];
/** Fetches the main dotnet module (<code>dotnet.js</code>). */
export declare function getMain(root?: string): Promise<ModuleAPI & {
    embedded?: boolean;
}>;
/** Fetches dotnet native module (<code>dotnet.native.js</code>). */
export declare function getNative(root?: string): Promise<unknown & {
    embedded?: boolean;
}>;
/** Fetches dotnet runtime module (<code>dotnet.runtime.js</code>). */
export declare function getRuntime(root?: string): Promise<unknown & {
    embedded?: boolean;
}>;
