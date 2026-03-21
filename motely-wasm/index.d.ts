export { MotelyWasm } from "./bootsharp/types/bindings.g";
export { Event, type EventSubscriber } from "./bootsharp/types/event";

/** Boot the .NET WASM runtime. Call once; subsequent calls return the same promise. */
export function boot(): Promise<unknown>;
