import type { Diagnostic } from "@codemirror/lint";
import * as Motely from "motely-wasm";
import { Jimmolate } from "motely-wasm";
import type { MotelySingleSearchContext } from "motely-wasm";

/**
 * The OG Immolate contract: `filter(inst) => score`. `inst` is the live
 * `MotelySingleSearchContext` interop instance (same context native C#
 * filters use), typed straight from motely-wasm's generated declarations.
 * Booleans coerce to 1/0; the engine keeps every seed whose score reaches
 * the cutoff (default 1).
 */
export type JimmolatePredicate = (
  inst: MotelySingleSearchContext
) => number | boolean;

export const DEFAULT_JIMMOLATE_SOURCE = `// Jimmolate — a JS-authored score provider run inside the engine, alongside
// your JAML must / should / mustNot clauses. Contract: filter(inst) => score.
// The engine keeps seeds whose score reaches the cutoff (default 1); booleans
// coerce to 1/0. inst is the live MotelySingleSearchContext — the same context
// native C# filters use. A real example, not a stub: score ante 1's first
// voucher, weighting the money engines.
const voucher = inst.getAnteFirstVoucher(1);
if (voucher === Motely.MotelyVoucher.SeedMoney) return 2;
if (voucher === Motely.MotelyVoucher.MoneyTree) return 3;
return 1;
`;

/**
 * Compiles Jimmolate predicate source (a function *body*, not a full function
 * declaration — same shape as DEFAULT_JIMMOLATE_SOURCE) into a callable.
 * Throws SyntaxError/ReferenceError synchronously if the source is broken;
 * callers should catch and surface that instead of wiring a bad predicate in.
 *
 * `new Function` here is not an injection point: this is a local, single-user
 * tool where the person typing the predicate and the person running the
 * search are the same person, in the same trust boundary as the JAML they
 * already wrote — no network input or other user's data ever reaches this.
 */
export function compileJimmolatePredicate(source: string): JimmolatePredicate {
  // eslint-disable-next-line @typescript-eslint/no-implied-eval -- this IS the feature: user JS run per-seed inside the search
  const fn = new Function("inst", "Motely", source);
  return ((inst: unknown) => fn(inst, Motely)) as JimmolatePredicate;
}

export function jimmolateLinter(source: string): Diagnostic[] {
  try {
    compileJimmolatePredicate(source);
    return [];
  } catch (err) {
    return [
      {
        from: 0,
        to: source.length,
        severity: "error",
        message: err instanceof Error ? err.message : String(err),
      },
    ];
  }
}

let currentPredicate: JimmolatePredicate = () => true;

function coerce(result: number | boolean): number {
  return typeof result === "number" ? Math.trunc(result) : result ? 1 : 0;
}

/**
 * Registers the Jimmolate JS bridge. Motely-wasm requires SOME binding to
 * exist before `bootsharp.boot()` — the bridge forms at boot(). This installs
 * a keep-all neutral binding that delegates to a swappable predicate, so the
 * UI can change behavior any time afterward via `setJimmolatePredicate`
 * without needing to rebind before another boot. Call once, before boot().
 */
export function bindJimmolateBridge(): void {
  Jimmolate.filter = (inst: MotelySingleSearchContext) => coerce(currentPredicate(inst));
}

export function setJimmolatePredicate(predicate: JimmolatePredicate): void {
  currentPredicate = predicate;
}
