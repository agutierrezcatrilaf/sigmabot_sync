using SigmabotConfig.Api.Auth;
using SigmabotConfig.Api.Configuration;
using SigmabotConfig.Api.Services;
using SigmabotSync.Domain.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(DatabaseSettings.SectionName));
builder.Services.Configure<CredencialesSettings>(builder.Configuration.GetSection(CredencialesSettings.SectionName));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(CorsSettings.SectionName));
builder.Services.Configure<AutorizadorSettings>(builder.Configuration.GetSection(AutorizadorSettings.SectionName));
builder.Services.AddSingleton<IDatabaseConnectionProvider, DatabaseConnectionProvider>();
builder.Services.AddSingleton<ICredencialClaveProtector>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CredencialesSettings>>().Value;
    return CredencialClaveProtectorFactory.CreateOptional(settings?.EncryptionKey);
});
builder.Services.AddScoped<IAutorizadorValidationService, AutorizadorValidationService>();
builder.Services.AddScoped<AuthFilter>();

var autorizadorUrl = builder.Configuration.GetSection(AutorizadorSettings.SectionName)
    .Get<AutorizadorSettings>()?.UrlApi?.Trim().TrimEnd('/');
builder.Services.AddHttpClient("Autorizador", client =>
{
    if (!string.IsNullOrWhiteSpace(autorizadorUrl))
    {
        client.BaseAddress = new Uri(autorizadorUrl + "/");
    }
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<AuthFilter>();
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SigmabotConfig API", Version = "v1" });
});

var corsOrigins = builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()?.AllowedOrigins
    ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Angular");
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
