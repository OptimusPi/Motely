import type { Diagnostic } from "@codemirror/lint";
import type { MotelySingleSearchContext } from "motely-wasm";
/**
 * The OG Immolate contract: `filter(inst) => score`. `inst` is the live
 * `MotelySingleSearchContext` interop instance (same context native C#
 * filters use), typed straight from motely-wasm's generated declarations.
 * Booleans coerce to 1/0; the engine keeps every seed whose score reaches
 * the cutoff (default 1).
 */
export type JimmolatePredicate = (inst: MotelySingleSearchContext) => number | boolean;
export declare const DEFAULT_JIMMOLATE_SOURCE = "// Jimmolate \u2014 a JS-authored score provider run inside the engine, alongside\n// your JAML must / should / mustNot clauses. Contract: filter(inst) => score.\n// The engine keeps seeds whose score reaches the cutoff (default 1); booleans\n// coerce to 1/0. inst is the live MotelySingleSearchContext \u2014 the same context\n// native C# filters use. A real example, not a stub: score ante 1's first\n// voucher, weighting the money engines.\nconst voucher = inst.getAnteFirstVoucher(1);\nif (voucher === Motely.MotelyVoucher.SeedMoney) return 2;\nif (voucher === Motely.MotelyVoucher.MoneyTree) return 3;\nreturn 1;\n";
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
export declare function compileJimmolatePredicate(source: string): JimmolatePredicate;
export declare function jimmolateLinter(source: string): Diagnostic[];
/**
 * Registers the Jimmolate JS bridge. Motely-wasm requires SOME binding to
 * exist before `bootsharp.boot()` — the bridge forms at boot(). This installs
 * a keep-all neutral binding that delegates to a swappable predicate, so the
 * UI can change behavior any time afterward via `setJimmolatePredicate`
 * without needing to rebind before another boot. Call once, before boot().
 */
export declare function bindJimmolateBridge(): void;
export declare function setJimmolatePredicate(predicate: JimmolatePredicate): void;
//# sourceMappingURL=jimmolatePredicate.d.ts.map