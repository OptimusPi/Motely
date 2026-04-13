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
    const config = MotelyWasmHost.loadJaml(
      JSON.stringify({ deck: "Red", stake: "White", must: [{ joker: "Blueprint" }] })
    );
    expect(config).toBeDefined();
    expect(config.deck).toBe(Motely.MotelyDeck.Red);
    expect(config.stake).toBe(Motely.MotelyStake.White);
  });

  it("throws on invalid JAML", () => {
    expect(() => MotelyWasmHost.loadJaml("not json")).toThrow();
  });

  it("starts random search from JAML", async () => {
    const jaml = JSON.stringify({
      deck: "Red",
      stake: "White",
      must: [{ joker: "Blueprint" }],
    });

    const results: { seed: string; score: number }[] = [];
    let completed = false;

    const onResult = (seed: string, score: number, _tally: Int32Array) => {
      results.push({ seed, score });
    };
    const onComplete = () => {
      completed = true;
    };

    SearchEvents.onResult.subscribe(onResult);
    SearchEvents.onComplete.subscribe(onComplete);

    try {
      await new Promise<void>((resolve, reject) => {
        const done = (status: string) => {
          SearchEvents.onResult.unsubscribe(onResult);
          SearchEvents.onComplete.unsubscribe(done);
          completed = true;
          if (status.startsWith("error:")) {
            reject(new Error(status));
            return;
          }
          resolve();
        };

        SearchEvents.onComplete.unsubscribe(onComplete);
        SearchEvents.onComplete.subscribe(done);
        MotelyWasmHost.startRandomSearchFromJaml(jaml, 1000);
      });
    } finally {
      SearchEvents.onResult.unsubscribe(onResult);
      SearchEvents.onComplete.unsubscribe(onComplete);
    }

    expect(completed).toBe(true);
    expect(results.length).toBeGreaterThan(0);
  }, 30_000);

  it("opens SingleSearchContext for seed exploration", () => {
    const ctx = MotelyWasmHost.motelySingleSearchContext("AAAAAAAA", Motely.MotelyDeck.Red, Motely.MotelyStake.White);
    expect(ctx).toBeDefined();

    const boss = ctx.getBossForAnte(1);
    expect(boss).toBeDefined();

    const voucher = ctx.getAnteFirstVoucher(1);
    expect(voucher).toBeDefined();

    const tag = ctx.getNextTag(1);
    expect(tag).toBeDefined();
  });
});
