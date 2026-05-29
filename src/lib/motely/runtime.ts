import bootsharp, { Motely } from "motely-wasm";

export type MotelyRuntimeStatus = "idle" | "booting" | "ready" | "error";

// Jimmolate probe dispatcher.
//
// Bootsharp snapshots [Import] bindings at boot() — assigning `Motely.jimmolateProbe`
// AFTER boot is a silent no-op, so the C# side calls an unbound import and the
// predicate never runs. The correct order (pre-boot bind, post-boot enable) is the
// one exercised by Motely.Wasm/tests/jimmolate.test.mjs, and the rule is in the
// Bootsharp docs: imported members "have to be assigned before booting the runtime."
//
// So we bind a STABLE dispatcher here at module load (this runs on import, always
// before any ensureMotelyReady()/boot() call) and swap the inner predicate per
// search via setJimmolateProbe(). enableJimmolate() is a C# [Export], so calling it
// after boot is fine — only this [Import] must be pre-bound.
type JimmolateProbe = typeof Motely.jimmolateProbe;
let currentProbe: JimmolateProbe = () => true;
Motely.jimmolateProbe = (seed, deck, stake) => currentProbe(seed, deck, stake);

/** Swap the active Jimmolate predicate. Safe before or after boot. */
export function setJimmolateProbe(pred: JimmolateProbe): void {
    currentProbe = pred;
}

/** Reset the probe to pass-through (the engine's default: every survivor matches). */
export function clearJimmolateProbe(): void {
    currentProbe = () => true;
}

// Must match the path the host serves motely-wasm's bin/ at.
// Used by main-thread hooks, workers, and Storybook staticDir alike.
// The Storybook staticDir in .storybook/main.ts serves it here.
// Next.js consumers must serve it at this path too (e.g. via a catch-all route).
// A bare "/bin" would 404 in every deployment context.
export const MOTELY_BIN_PATH = "/motely-wasm/bin";

export async function ensureMotelyReady(): Promise<void> {
    if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
        await bootsharp.boot(MOTELY_BIN_PATH);
    }
}
