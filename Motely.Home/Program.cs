using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var mimeTypes = new FileExtensionContentTypeProvider();
mimeTypes.Mappings[".mjs"] = "application/javascript";
mimeTypes.Mappings[".wasm"] = "application/wasm";

var wasmDist = FindWasmDist();
if (wasmDist is not null)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(wasmDist),
        RequestPath = "/wasm",
        ContentTypeProvider = mimeTypes,
    });
}
else
{
    app.Logger.LogWarning("motely-wasm dist not found — build it first: dotnet publish ../Motely.Wasm -c Release");
}

app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = mimeTypes });

app.MapGet("/", () => Results.Redirect("/app"));
app.MapGet("/app", () => Results.Content(
    File.ReadAllText(Path.Combine(builder.Environment.WebRootPath, "app.html")),
    "text/html; charset=utf-8"));

app.Run();

static string? FindWasmDist()
{
    string[] candidates =
    [
        Path.Combine(Directory.GetCurrentDirectory(), "../Motely.Wasm/dist"),
        Path.Combine(AppContext.BaseDirectory, "../../../../Motely.Wasm/dist"),
    ];
    return candidates.Select(Path.GetFullPath).FirstOrDefault(Directory.Exists);
}
