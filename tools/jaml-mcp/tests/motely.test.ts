import { describe, it, expect, beforeAll } from "vitest";
import bootsharp, { MotelyJamlSearchBuilder, Motely, MotelySingleSearchContext, SearchEvents } from "motely-wasm";

beforeAll(async () => {
  await bootsharp.boot();
}, 30_000);

describe("motely-wasm", () => {
  it("boots and returns version", () => {
    const ver = MotelyJamlSearchBuilder.getVersion();
    expect(typeof ver).toBe("string");
    expect(ver).toMatch(/^\d+\.\d+/);
  });

  it("loads valid JAML", () => {
    const config = MotelyJamlSearchBuilder.loadJaml(
      JSON.stringify({ deck: "Red", stake: "White", must: [{ joker: "Blueprint" }] })
    );
    expect(config).toBeDefined();
    expect(config.deck).toBe(Motely.MotelyDeck.Red);
    expect(config.stake).toBe(Motely.MotelyStake.White);
  });

  it("throws on invalid JAML", () => {
    expect(() => MotelyJamlSearchBuilder.loadJaml("not json")).toThrow();
  });

  // FAILING: Runtime crashes with process.exit(1)
  // See: https://github.com/OptimusPi/MotelyJAML/issues/TODO
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

    const session = MotelyJamlSearchBuilder.loadJaml(jaml).random(1000).run();
    await session.waitForCompletionAsync(null);

    SearchEvents.onResult.unsubscribe(onResult);
    SearchEvents.onComplete.unsubscribe(onComplete);

    expect(completed).toBe(true);
    expect(results.length).toBeGreaterThan(0);
  }, 30_000);

  // FAILING: Runtime crashes with process.exit(1)
  // See: https://github.com/OptimusPi/MotelyJAML/issues/TODO
  it("opens SingleSearchContext for seed exploration", () => {
    const ctx = MotelySingleSearchContext.open("AAAAAAAA", Motely.MotelyDeck.Red, Motely.MotelyStake.White);
    expect(ctx).toBeDefined();

    const boss = ctx.getBossForAnte(1);
    expect(boss).toBeDefined();

    const voucher = ctx.getAnteFirstVoucher(1);
    expect(voucher).toBeDefined();

    const tag = ctx.getNextTag(1);
    expect(tag).toBeDefined();
  });
});
