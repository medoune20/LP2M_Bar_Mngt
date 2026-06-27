using GestionHopital.Infrastructure.Donnees;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
var dataDir = Environment.GetEnvironmentVariable("DATA_DIR")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GestionHopital");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "hopital.db");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

// MVC + Session
builder.Services.AddControllersWithViews();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromHours(8);
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
    opt.Cookie.SameSite = SameSiteMode.Lax;
    opt.Cookie.Name = ".GestionHopital.Session";
});
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Init DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseInitializer.InitialiserAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Accueil/Erreur");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Accueil}/{action=Index}/{id?}");

app.MapGet("/health", () => Results.Ok(new { statut = "ok", heure = DateTime.UtcNow }));

app.Run();
