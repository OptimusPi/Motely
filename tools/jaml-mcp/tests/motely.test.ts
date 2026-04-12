/**
 * motely-wasm-compat unit tests (vitest).
 *
 * Run from tools/jaml-mcp/: `pnpm test`
 *
 * These tests document the contract the WASM package must honor for the MCP
 * server to function. If a test fails after a build, the WASM bridge is
 * broken and the MCP cannot serve real seeds.
 *
 * Notes:
 * - We use `motely-wasm-compat` because that's what the MCP server depends on
 *   (api/tools.ts). The full `motely-wasm` package shares the same API
 *   surface for everything tested here.
 * - We deliberately avoid the impl-side stateful methods that track per-ante
 *   state inside C# (e.g. `getBossForAnte(ante)` walking `_lastBossAnte`).
 *   Those rely on Bootsharp DI singleton resolution which is fragile across
 *   instances. We test the host-level methods (`MotelyWasmHost.singleGet*`)
 *   that don't return a stateful handle, and treat boot/loadJaml/getVersion
 *   as the smoke contract.
 */
import { describe, it, expect, beforeAll } from "vitest";
import dotnet, { MotelyWasmHost, SearchEvents } from "motely-wasm-compat";

beforeAll(async () => {
  await dotnet.boot();
}, 60_000);

const MINIMAL_JAML = JSON.stringify({
  deck: "Red",
  stake: "White",
  must: [{ joker: "Blueprint", antes: [1] }],
});

describe("motely-wasm: smoke (proves boot + basic interop work)", () => {
  it("getVersion returns a non-empty semver-shaped string", () => {
    const ver = MotelyWasmHost.getVersion();
    expect(typeof ver).toBe("string");
    expect(ver).toMatch(/^\d+\.\d+\.\d+/);
  });

  it("loadJaml parses a valid JAML config and exposes deck + stake", () => {
    const config = MotelyWasmHost.loadJaml(MINIMAL_JAML);
    expect(config).toBeDefined();
    expect(config.deck).toBeDefined();
    expect(config.stake).toBeDefined();
  });

  it("loadJaml throws on invalid JSON", () => {
    expect(() => MotelyWasmHost.loadJaml("not-json{{")).toThrow();
  });
});

describe("motely-wasm: search pipeline (the path that crashed in 9.0.0)", () => {
  it(
    "startRandomSearchFromJaml runs a small random search end-to-end without crashing the runtime",
    async () => {
      // The 9.0.0 bug surfaced here: this method internally called other
      // [JSExport] interface members on `this` (LoadJaml + StartRandomSearch),
      // and Mono WASM rejected the resulting managed→[UnmanagedCallersOnly]
      // dispatch with: "Fatal error. Invalid Program: attempted to call a
      // UnmanagedCallersOnly method from managed code."
      //
      // Fix: MotelyWasmHost.cs *FromJaml methods now inline LoadJamlCore (a
      // private static helper) instead of calling LoadJaml on `this`.

      // We observe completion via the SearchEvents stream rather than
      // awaiting `session.waitForCompletionAsync(...)` on the returned handle.
      // Both should work after the C# fix, but the event-stream path is what
      // the MCP server uses in production (api/tools.ts::searchSeeds). If that
      // works, we know the pipeline is healthy end-to-end.
      let resultCount = 0;
      const completion = await new Promise<{
        status: string;
        searched: bigint;
        matching: bigint;
      }>((resolve, reject) => {
        const onResult = () => {
          resultCount++;
        };
        const onComplete = (status: string, searched: bigint, matching: bigint) => {
          SearchEvents.onResult.unsubscribe(onResult);
          SearchEvents.onComplete.unsubscribe(onComplete);
          resolve({ status, searched, matching });
        };
        SearchEvents.onResult.subscribe(onResult);
        SearchEvents.onComplete.subscribe(onComplete);
        try {
          MotelyWasmHost.startRandomSearchFromJaml(MINIMAL_JAML, 1000);
        } catch (err) {
          SearchEvents.onResult.unsubscribe(onResult);
          SearchEvents.onComplete.unsubscribe(onComplete);
          reject(err);
        }
      });

      expect(completion.status).toBe("completed");
      expect(completion.searched).toBeGreaterThanOrEqual(1n);
      // Either matches or no matches is fine for a 1k-seed budget; we just
      // require the pipeline to run and emit a structured completion.
      expect(typeof completion.matching).toBe("bigint");
      expect(resultCount).toBeGreaterThanOrEqual(0);
    },
    60_000,
  );
});

describe("motely-wasm: single-seed inspection via host-level wrappers", () => {
  // We use the host-level `single*` methods rather than holding a
  // `MotelySingleSearchContext` handle on the JS side. The handle pattern
  // routes through Bootsharp DI singleton resolution and is fragile; the
  // host-level methods take seed/deck/stake on every call and do not rely
  // on a per-instance handle.

  it("singleGetBossForAnte returns a defined boss for ante 1", () => {
    // 0 = MotelyDeck.Red, 0 = MotelyStake.White (numeric enum values).
    const boss = MotelyWasmHost.singleGetBossForAnte("AAAAAAAA", 0, 0, 1);
    expect(boss).toBeDefined();
  });

  it("singleGetAnteFirstVoucher returns a defined voucher for ante 1", () => {
    const voucher = MotelyWasmHost.singleGetAnteFirstVoucher("AAAAAAAA", 0, 0, 1);
    expect(voucher).toBeDefined();
  });
});
