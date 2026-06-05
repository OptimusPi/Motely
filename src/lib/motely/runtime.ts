import bootsharp from "motely-wasm";
import { Program as Motely } from "motely-wasm/motely/wasm";
import type { MotelySingleSearchContext } from "motely-wasm/motely";

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
// motely-wasm 19.4.0 changed the probe to receive a search context instead of
// (seed, deck, stake). We keep the inner predicate contract identical for all
// callers and bridge to the new ctx shape in this one place.
type JimmolateProbe = (seed: string, deck: number, stake: number) => boolean;
let currentProbe: JimmolateProbe = () => true;
Motely.jimmolateProbe = (ctx: MotelySingleSearchContext) =>
    currentProbe(ctx.getSeed(), ctx.deck, ctx.stake);

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
        // motely-wasm is an EMBEDDED build (the runtime is inlined into the JS as
        // base64 — see dist/generated/resources.g.mjs), so boot() takes no args and
        // needs no served binaries. The old boot("/motely-wasm/bin") was leftover
        // sideloaded config and 404'd in every context.
        await bootsharp.boot();
    }
}
