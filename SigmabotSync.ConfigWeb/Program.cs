using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using MudBlazor.Services;
using SigmabotSync.ConfigWeb.Services;
using SigmabotSync.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddSingleton<SettingsService>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var path = Path.Combine(env.ContentRootPath, "settings.json");
    return new SettingsService(path);
});
builder.Services.AddScoped<IConfiguratorDialogs, MudConfiguratorDialogs>();
builder.Services.AddScoped<ConfiguratorState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();
