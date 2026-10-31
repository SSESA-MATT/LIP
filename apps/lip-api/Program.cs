using Lip.Api.Data;
using Lip.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var connection = builder.Configuration.GetConnectionString("Lip");
builder.Services.AddDbContext<LipDb>(options =>
{
    if (string.IsNullOrWhiteSpace(connection) || connection.Contains("Mode=Sqlite", StringComparison.OrdinalIgnoreCase) || connection.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
    {
        var path = string.IsNullOrWhiteSpace(connection) || connection.Contains("Mode=Sqlite", StringComparison.OrdinalIgnoreCase)
            ? "Data Source=lip-demo.db"
            : connection;
        options.UseSqlite(path);
    }
    else
    {
        options.UseSqlServer(connection);
    }
});

var origins = (builder.Configuration["Cors:Origins"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LipDb>();
    db.Database.EnsureCreated();
    DemoSeed.EnsureCommandThreadColumn(db);
    DemoSeed.Apply(db);
}

if (app.Environment.IsDevelopment()) app.MapOpenApi();
LipEndpoints.Map(app);
app.Run();

public partial class Program;
