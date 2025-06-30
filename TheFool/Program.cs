

using TheFool.Components;
using TheFool.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor services
builder.Services.AddMudServices();

// Add application services
builder.Services.AddSingleton<IDuckDbService, DuckDbService>();
builder.Services.AddSingleton<IProcessRunnerService, ProcessRunnerService>();

// Configure paths
builder.Configuration["DuckDbPath"] = "ouija_databases";

var app = builder.Build();

// Ensure database directory exists
Directory.CreateDirectory("ouija_databases");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Cleanup on shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var processRunner = app.Services.GetService<IProcessRunnerService>();
    (processRunner as IDisposable)?.Dispose();
    
    var duckDb = app.Services.GetService<IDuckDbService>();
    (duckDb as IDisposable)?.Dispose();
});

app.Run();
