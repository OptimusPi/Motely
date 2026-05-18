import bootsharp from "motely-wasm";

export type MotelyRuntimeStatus = "idle" | "booting" | "ready" | "error";

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
