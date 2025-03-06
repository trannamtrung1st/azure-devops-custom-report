using AdoReport.WorkerApp;
using AdoReport.WorkerApp.Data;
using AdoReport.WorkerApp.Data.Extensions;
using AdoReport.WorkerApp.Services;
using AdoReport.WorkerApp.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(nameof(AppDbContext))));

builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<IAzureDevOpsService, AzureDevOpsService>();

var host = builder.Build();

// Apply migrations at startup
await host.MigrateDatabaseAsync();

await host.RunAsync();
