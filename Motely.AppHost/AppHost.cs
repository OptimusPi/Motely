// Motely under .NET Aspire. `aspire run` (or `dotnet run --project Motely.AppHost`) brings up every
// long-running piece with one dashboard for logs, traces and metrics:
//
//   helper-api            Motely.HelperAPI on its usual http://localhost:3141 — /health, /api/validate,
//                         /api/search (Launchpad multi-search). Port comes from its launch profile.
//   distributed-worker    MotelyWorker in Search Party mode against seedfinder.app. Explicit start:
//                         set the `party-id` parameter in the dashboard, then start the resource.
//   jaml-ui               Storybook of the sibling jaml-ui checkout, when one is present.

var builder = DistributedApplication.CreateBuilder(args);

// Under Aspire every project runs with its own project directory as cwd, so the engine's relative
// defaults ("JamlFilters", "Seeds") would resolve under Motely.HelperAPI/ and scatter the
// lakes. Hand every resource the repo's own directories instead.
var repoRoot   = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, ".."));
var filtersDir = Path.Combine(repoRoot, "JamlFilters");
var seedsDir   = Path.Combine(repoRoot, "Seeds");


// ── helper-api ────────────────────────────────────────────────────────────────────────────────
// Its appsettings.json points the in-process pool worker at https://www.seedfinder.app; under the
// dev dashboard that worker stays idle unless this parameter is set (PoolWorkerHostedService returns
// at once on a blank Url). Setting it to the seedfinder URL makes helper-api grind the public pool.
var poolUrl = builder.AddParameter("seedfinder-pool-url", "");

var helperApi = builder.AddProject<Projects.Motely_HelperAPI>("helper-api")
    // No WithHttpEndpoint here: the http endpoint on 3141 is created from Motely.HelperAPI's launch
    // profile (applicationUrl), and a second explicit "http" endpoint would collide with it.
    .WithEnvironment("Pool__Url", poolUrl)
    .WithEnvironment("Pool__LocalDbPath", seedsDir)
    .WithEnvironment("MOTELY_FILTERS_DIR", filtersDir)
    .WithHttpHealthCheck("/health");

// ── distributed-worker ────────────────────────────────────────────────────────────────────────
// Pool mode (`--pool <url>`) claims blocks from POST {url}/api/search/helper — a coordinator
// HelperAPI does not implement (its /api/search is the Launchpad, not a block queue) — so the
// worker is deliberately not wired to helper-api. Party mode against seedfinder.app is the one
// that works end to end, and it needs a party id a person chooses, hence explicit start.
var partyId     = builder.AddParameter("party-id", "");
var partyServer = builder.AddParameter("party-server", "https://www.seedfinder.app");

builder.AddProject<Projects.Motely_DistributedWorker>("distributed-worker")
    .WithArgs("--party", partyId, "--server", partyServer, "--local-db", seedsDir)
    .WithExplicitStart();

// ── jaml-ui ───────────────────────────────────────────────────────────────────────────────────
// jaml-ui is a component library (vite build → dist/*.js); Storybook is its browsable UI. The
// `dev` script is `storybook dev` — the port is passed as an argument because Storybook ignores
// PORT, and the endpoint is unproxied so the declared port is the one Storybook actually binds.
// The `jaml-ui` submodule entry is not checked out in this tree; the sibling checkout is used.
var jamlUiDir = Path.GetFullPath(Path.Combine(repoRoot, "..", "jaml-ui"));
if (Directory.Exists(jamlUiDir))
{
    builder.AddJavaScriptApp("jaml-ui", jamlUiDir)
        .WithPnpm()
        .WithRunScript("dev", ["--port", "6006", "--no-open"])
        .WithHttpEndpoint(port: 6006, isProxied: false);
}

builder.Build().Run();
