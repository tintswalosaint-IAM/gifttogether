using GiftTogether.Data;
using GiftTogether.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=gifttogether.db"));
builder.Services.AddSingleton<TokenService>();

var app = builder.Build();

// Apply any pending migrations automatically on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Ensure the uploads directory exists so static file serving works
var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

// Guest registry route: /r/{slug} → serve /r/index.html
app.MapFallback("/r/{slug}", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(
        Path.Combine(app.Environment.WebRootPath, "r", "index.html"));
});

// SPA fallback
app.MapFallbackToFile("index.html");

app.Run();
