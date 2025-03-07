using AdoReport.WorkerApp;
using AdoReport.WorkerApp.Persistence;
using AdoReport.WorkerApp.Services;
using AdoReport.WorkerApp.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
var services = builder.Services;

services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(nameof(AppDbContext))));

services.AddHostedService<Worker>()
    .AddScoped<IAzureDevOpsService, AzureDevOpsService>();

var host = builder.Build();

await host.RunAsync();
