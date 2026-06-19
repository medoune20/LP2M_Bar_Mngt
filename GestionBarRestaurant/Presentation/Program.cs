using System.Globalization;
using Infrastructure.Donnees;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Hubs;

var builder = WebApplication.CreateBuilder(args);

var cultureInvariante = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = cultureInvariante;
CultureInfo.DefaultThreadCurrentUICulture = cultureInvariante;

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<Presentation.Filtres.AccesModuleFiltre>();
    // Avec <Nullable>enable</Nullable>, MVC rend obligatoire toute propriété string
    // non-nullable. On désactive ce comportement : seuls les [Required] explicites
    // sont exigés (ex. l'établissement n'impose que le Nom).
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<Infrastructure.Services.EmailService>();

// Messagerie temps réel (SignalR) cloisonnée par tenant.
builder.Services.AddSignalR();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var googleConfigured = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret);

var authBuilder = builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".GestionBar.ExternalAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Auth/Connexion";
    });

if (googleConfigured)
{
    authBuilder.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = googleClientId!;
        options.ClientSecret = googleClientSecret!;
        options.SaveTokens = false;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    });
}

var dataDirectory = Environment.GetEnvironmentVariable("DATA_DIR")
    ?? Path.Combine(builder.Environment.ContentRootPath, "Data");
Directory.CreateDirectory(dataDirectory);
var dbPath = Path.Combine(dataDirectory, "gestionbar_analytics_v3.db");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".GestionBar.Session";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DatabaseInitializer.Initialiser(db);
    NormaliserSQLiteDecimal.Executer(db);
}

app.UseForwardedHeaders();

var pathBase = Environment.GetEnvironmentVariable("PATH_BASE");
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Accueil/Erreur");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Accueil}/{action=Index}/{id?}");

// Hub temps réel de la messagerie interne par établissement.
app.MapHub<ChatHub>("/hubChat");

// Sonde de disponibilité pour les déploiements (Docker / reverse proxy).
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));

app.Run();
