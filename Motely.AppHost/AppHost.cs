var builder = DistributedApplication.CreateBuilder(args);

// Postgres — DuckLake's catalog store (multi-writer concurrency for the seed data lake).
// No dependent resource references it yet — SeedLakeSink still writes per-filter .duckdb files;
// this just makes a real Postgres available in the dashboard to develop the DuckLake catalog against.
var postgres = builder.AddPostgres("postgres").WithDataVolume();
postgres.AddDatabase("ducklake-catalog");

var helperApi = builder.AddProject<Projects.Motely_HelperAPI>("helper-api");

builder
    .AddProject<Projects.Motely_DistributedWorker>("distributed-worker")
    .WithReference(helperApi)
    .WaitFor(helperApi);

// jaml-ui is a component library (vite build → dist/*.js), not a dev-server app — there is no
// `vite dev` script. Storybook is its actual browsable UI, hardcoded to port 3141 in package.json
// ("storybook": "pnpm run build && storybook dev -p 3141"), so the endpoint here matches that
// literal port rather than an env-injected one.
builder
    .AddJavaScriptApp("jaml-ui", "../../jaml-ui")
    .WithPnpm()
    .WithRunScript("storybook")
    .WithHttpEndpoint(port: 3141);

builder.Build().Run();
