import { describe, it, expect, beforeAll } from "vitest";
import bootsharp, { MotelyWasmHost } from "motely-wasm";
import { bootPromise, searchSeeds, analyzeSeed } from "../api/tools.js";

beforeAll(async () => {
  await bootPromise;
}, 30_000);

describe("motely-wasm integration", () => {
  it("boots successfully", async () => {
    expect(await bootPromise).toBeUndefined();
  });

  it("getVersion returns version string", () => {
    const version = MotelyWasmHost.getVersion();
    expect(typeof version).toBe("string");
    expect(version).toMatch(/^\d+\.\d+/);
  });

  it("loadJaml parses valid JAML", () => {
    const config = MotelyWasmHost.loadJaml('{"deck":"Red","stake":"White"}');
    expect(config).toBeDefined();
    expect(config.deck).toBe(1); // MotelyDeck.Red
    expect(config.stake).toBe(1); // MotelyStake.White
  });

  it("searchSeeds returns results", async () => {
    const jaml = JSON.stringify({
      deck: "Red",
      stake: "White",
      must: [{ joker: "Joker", antes: [1] }],
    });
    const result = await searchSeeds(jaml, 1000);
    expect(result).toBeDefined();
    expect(result.status).toBeDefined();
    expect(result.matchesFound).toBeDefined();
    expect(result.seedsSearched).toBeDefined();
    expect(Array.isArray(result.results)).toBe(true);
  }, 30_000);

  it("analyzeSeed returns structured ante data", async () => {
    const jaml = JSON.stringify({ deck: "Red", stake: "White" });
    const ctx = await analyzeSeed("TEST1234", jaml);
    expect(ctx).toBeDefined();
    expect(ctx.seed).toBe("TEST1234");
    expect(Array.isArray(ctx.antes)).toBe(true);
    expect(ctx.antes.length).toBe(8);
  }, 30_000);
});
