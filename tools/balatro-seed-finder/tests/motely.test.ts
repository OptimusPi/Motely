import { execFileSync } from "node:child_process";
import { describe, it, expect, beforeAll } from "vitest";
import bootsharp, { MotelyWasmHost, Motely, SearchEvents } from "motely-wasm";

beforeAll(async () => {
  await bootsharp.boot();
}, 30_000);

describe("motely-wasm", () => {
  it("boots and returns version", () => {
    const ver = MotelyWasmHost.getVersion();
    expect(typeof ver).toBe("string");
    expect(ver).toMatch(/^\d+\.\d+/);
  });

  it("loads valid JAML", () => {
    const configId = MotelyWasmHost.loadJaml(
      JSON.stringify({ deck: "Red", stake: "White", must: [{ joker: "Blueprint" }] })
    );
    expect(typeof configId).toBe("string");
    expect(configId.length).toBeGreaterThan(0);
  });

  it("throws on invalid JAML", () => {
    const stdout = execFileSync(
      "node",
      [
        "--input-type=module",
        "-e",
        "import bootsharp,{MotelyWasmHost} from 'motely-wasm'; await bootsharp.boot(); try { MotelyWasmHost.loadJaml('not json'); console.log('no-throw'); process.exit(2); } catch { console.log('threw'); }",
      ],
      {
        cwd: process.cwd(),
        encoding: "utf8",
      }
    );

    expect(stdout).toContain("threw");
  });

  it("starts random search from JAML", async () => {
    const jaml = JSON.stringify({
      deck: "Red",
      stake: "White",
      must: [{ joker: "Blueprint" }],
      should: [{ joker: "Brainstorm" }], // Bluestorm <3 
    });

    const results: { seed: string; score: number }[] = [];
    let completed = false;

    function onResult(seed: string, score: number, _tally: Int32Array): void {
      results.push({ seed, score });
    }
    await new Promise<void>((resolve, reject) => {
      function onComplete(status: string): void {
        SearchEvents.onResult.unsubscribe(onResult);
        SearchEvents.onComplete.unsubscribe(onComplete);
        completed = true;
        if (status.startsWith("error:")) {
          reject(new Error(status));
          return;
        }
        resolve();
      }

      SearchEvents.onResult.subscribe(onResult);
      SearchEvents.onComplete.subscribe(onComplete);

      try {
        MotelyWasmHost.startRandomSearchFromJaml(jaml, 1000);
      } catch (err) {
        SearchEvents.onResult.unsubscribe(onResult);
        SearchEvents.onComplete.unsubscribe(onComplete);
        reject(err);
      }
    });

    expect(completed).toBe(true);
    expect(results.length).toBeGreaterThan(0);
  }, 30_000);

  it("opens SingleSearchContext for seed exploration", () => {
    const ctxId = MotelyWasmHost.openSingleSearchContext("AAAAAAAA", Motely.MotelyDeck.Red, Motely.MotelyStake.White);
    expect(typeof ctxId).toBe("string");

    const seed = MotelyWasmHost.contextGetSeed(ctxId);
    expect(seed).toBe("AAAAAAAA");

    const boss = MotelyWasmHost.contextGetBossForAnte(ctxId, 1);
    expect(boss).toBeDefined();

    const voucher = MotelyWasmHost.contextGetAnteFirstVoucher(ctxId, 1);
    expect(voucher).toBeDefined();

    const tag = MotelyWasmHost.contextGetNextTag(ctxId, 1);
    expect(tag).toBeDefined();

    MotelyWasmHost.contextClose(ctxId);
  });
});
