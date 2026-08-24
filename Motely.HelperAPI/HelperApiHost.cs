using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Motely.DistributedWorker;

namespace Motely.HelperAPI;

/// <summary>
/// Motely API host — built once here, run two ways: as the standalone
/// <c>Motely.HelperAPI</c> executable (<see cref="Program"/>'s <c>app.Run()</c>), and in-process
/// from <c>Motely.TUI</c>'s ApiServerWindow (<c>StartAsync</c>/<c>WaitForShutdownAsync</c>/
/// <c>StopAsync</c> for interactive start/stop). One implementation, two callers.
/// </summary>
public static class HelperApiHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ── Aspire service defaults — OTel traces/metrics, service discovery, HTTP resilience.
        //    Not calling MapDefaultEndpoints() below: it would register its own "/health" via
        //    MapHealthChecks, colliding with this file's existing custom "/health" (which also
        //    reports MotelyVersion) — two GET handlers on the same path is an ambiguous match
        //    at request time, not a build-time error.
        builder.AddServiceDefaults();

        // ── AOT-safe JSON ───────────────────────────────────────────────────
        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Add(HelperApiJsonContext.Default));

        // ── CORS — open for community tools ─────────────────────────────────
        builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
            p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

        // ── Distributed Worker (in-process) ─────────────────────────────────
        builder.Services.Configure<PoolWorkerOptions>(
            builder.Configuration.GetSection(PoolWorkerOptions.SectionName));
        builder.Services.AddHostedService<PoolWorkerHostedService>();

        var app = builder.Build();

        // ── Middleware pipeline ─────────────────────────────────────────────

        app.UseCors();

        // ── MIME type provider — WASM needs application/wasm or the browser
        //    will refuse to compile it. Also explicitly map .mjs, .br, .gz.
        var mimeProvider = new FileExtensionContentTypeProvider();
        mimeProvider.Mappings[".wasm"]  = "application/wasm";
        mimeProvider.Mappings[".mjs"]   = "text/javascript";
        mimeProvider.Mappings[".cjs"]   = "text/javascript";
        mimeProvider.Mappings[".br"]    = "application/x-brotli";
        mimeProvider.Mappings[".gz"]    = "application/x-gzip";
        mimeProvider.Mappings[".blat"]  = "application/octet-stream";
        mimeProvider.Mappings[".dat"]   = "application/octet-stream";
        mimeProvider.Mappings[".sym"]   = "application/octet-stream";

        // ── Root wwwroot (if present — splash page, assorted assets) ────────
        if (Directory.Exists(builder.Environment.WebRootPath))
        {
            var staticFileOptions = new StaticFileOptions
            {
                ContentTypeProvider = mimeProvider,
                ServeUnknownFileTypes = true,  // Catch any edge-case extensions (pdb, etc.)
                // Enable headers that let pre-compressed files be served correctly.
                OnPrepareResponse = ctx =>
                {
                    var path = ctx.File.Name;
                    if (path.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Context.Response.Headers.ContentEncoding = "br";
                        // Strip the .br so the browser gets the original content-type
                        var baseName = path[..^3];
                        if (mimeProvider.TryGetContentType(baseName, out var baseMime))
                            ctx.Context.Response.ContentType = baseMime;
                    }
                    else if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Context.Response.Headers.ContentEncoding = "gzip";
                        var baseName = path[..^3];
                        if (mimeProvider.TryGetContentType(baseName, out var baseMime))
                            ctx.Context.Response.ContentType = baseMime;
                    }
                    // No COOP/COEP here on purpose: motely-wasm is Bootsharp AOT (LLVM-native,
                    // SIMD), not SharedArrayBuffer-threaded, so cross-origin isolation buys us
                    // nothing and only complicates embedding the app.
                },
            };

            app.UseDefaultFiles();
            app.UseStaticFiles(staticFileOptions);
        }

        // ── Routes ──────────────────────────────────────────────────────────

        // Motely.dll has no product version (GenerateAssemblyInfo=false). Expose repo MotelyVersion via *this* assembly (GenerateAssemblyInfo=true + Directory.Build.props Version).
        var motelyVersion = GetHelperApiMotelyVersion();

        app.MapGet("/health", () =>
            Results.Ok(new HealthResponse("ok", motelyVersion)));

        app.MapGet("/api/version", () =>
            Results.Ok(new VersionResponse(
                Api: "Motely.HelperAPI",
                Motely: motelyVersion,
                Runtime: System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
            )));

        app.MapGet("/api/worker/status", (IOptions<PoolWorkerOptions> opts) =>
        {
            var o = opts.Value;
            return Results.Ok(new WorkerStatusResponse(
                Enabled: !string.IsNullOrWhiteSpace(o.Url),
                PoolUrl: o.Url,
                Threads: o.Threads,
                WorkerId: o.WorkerId,
                FilterId: o.FilterId
            ));
        });

        // ── JAML validation — the engine's own loader is the ground truth, so JAMMY
        //    (or any client) validates against exactly what the searcher accepts.
        app.MapPost("/api/validate", (ValidateRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Jaml))
                return Results.BadRequest(new ErrorResponse("Missing jaml text."));

            bool ok = Motely.Filters.Jaml.JamlConfigLoader.TryLoad(
                req.Jaml, out var config, out string? error);

            var errors = new List<string>();
            if (!ok && error is not null)
                errors.Add(error);
            if (ok && config is not null)
            {
                if (config.Must.Count + config.Should.Count + config.MustNot.Count == 0)
                    errors.Add("warning: no must/should/mustNot clauses — this filter matches every seed.");
            }
            return Results.Ok(new ValidateResponse(ok && errors.Count == 0, errors));
        });

        // ── Multi-search (Motely Launchpad) ─────────────────────────────────
        // The CLI/TUI run one filter at a time; these endpoints run K JAML filters
        // concurrently in-process, each with its own engine thread pool, and expose
        // live progress + found seeds for the dashboard in wwwroot.

        app.MapGet("/api/filters", () => Results.Ok(SearchJobManager.ListFilters()));

        app.MapGet("/api/filters/{name}", (string name) =>
            SearchJobManager.ReadFilterJaml(name) is { } jaml
                ? Results.Text(jaml, "text/plain")
                : Results.NotFound(new ErrorResponse($"Filter '{name}' not found.")));

        app.MapPost("/api/search", (StartSearchRequest req) =>
        {
            try
            {
                return Results.Ok(new StartSearchResponse(SearchJobManager.Start(req)));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message));
            }
        });

        app.MapGet("/api/search", () => Results.Ok(SearchJobManager.ListJobs()));

        app.MapGet("/api/search/{id}", (string id) =>
            SearchJobManager.GetResults(id) is { } results
                ? Results.Ok(results)
                : Results.NotFound(new ErrorResponse($"Job '{id}' not found.")));

        app.MapPost("/api/search/{id}/stop", (string id) =>
            SearchJobManager.Stop(id)
                ? Results.Ok(new StopSearchResponse(true))
                : Results.NotFound(new ErrorResponse($"Job '{id}' not found.")));

        return app;
    }

    private static string GetHelperApiMotelyVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (info?.InformationalVersion is { Length: > 0 } s)
            return s.Split('+')[0].Trim();
        return asm.GetName().Version?.ToString(3) ?? "7.0.0";
    }
}

// ── Response records ────────────────────────────────────────────────

record HealthResponse(string Status, string MotelyVersion);
record VersionResponse(string Api, string Motely, string Runtime);
record WorkerStatusResponse(bool Enabled, string PoolUrl, int Threads, string WorkerId, string FilterId);
