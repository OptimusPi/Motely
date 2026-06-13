import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { Motely } = harness;

try {
    console.log("Running seed-list search...");
    const cfg = Motely.fromYaml(jaml.anyMust);
    cfg.seeds = ["AAAAAAAA", "BBBBBBBB"];
    const r = Motely.runSeedListSearch(cfg);
    console.log("Result:", {
        isCompleted: r.isCompleted,
        totalSeedsSearched: Number(r.totalSeedsSearched),
        matchingSeeds: Number(r.matchingSeeds),
        elapsedMs: Number(r.elapsedMs),
    });
} catch (err) {
    console.error("Caught error in JS:", err);
    if (err.stack) console.error("Stack trace:", err.stack);
}
