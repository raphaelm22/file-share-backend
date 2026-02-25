using System.Text.Json;
using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Http;
using FileShare.Infrastructure.Data;
using FileShare.Infrastructure.FileSystem;
using FileShare.Infrastructure.Hubs;
using FileShare.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Host.UseWolverine();

builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddSingleton<FileStateTracker>();
builder.Services.AddHostedService<FileSystemWatcherService>();
builder.Services.AddHostedService<PollingService>();

builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=data/fileshare.db"));

builder.Services.AddSingleton<SasTokenService>();

builder.Services.AddScoped(typeof(IRepositoryBase<>), typeof(EfRepository<>));
builder.Services.AddScoped(typeof(IReadRepositoryBase<>), typeof(EfRepository<>));

builder.Services.AddWolverineHttp();

builder.Services.AddOpenApi();

var app = builder.Build();

app.MigrateDatabase();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapWolverineEndpoints();
app.MapHub<FileShareHub>("/hubs/fileshare");

app.Run();
