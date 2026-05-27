import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { Motely } = harness;

async function run() {
    try {
        console.log("Creating search settings...");
        const settings = Motely.fromJaml(jaml.anyMust)
            .withListSearch(["AAAAAAAA", "BBBBBBBB"], 2)
            .withThreadCount(1);
        
        console.log("Starting search...");
        const search = settings.start();
        console.log("Search object returned:", search);
        console.log("Search ID:", search._id);
        console.log("Search isCompleted:", search.isCompleted);
        console.log("Search elapsedMs:", search.elapsedMs);
        
        console.log("Awaiting search completion...");
        await search.waitForCompletionAsync();
        console.log("Search completed successfully!");
    } catch (err) {
        console.error("Caught error in JS:", err);
        if (err.stack) {
            console.error("Stack trace:", err.stack);
        }
    }
}

run().catch(console.error);
