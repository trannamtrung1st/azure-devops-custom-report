using AdoReport.WorkerApp;
using AdoReport.WorkerApp.Services;
using AdoReport.WorkerApp.Services.Abstractions;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<IAzureDevOpsService, AzureDevOpsService>();

var host = builder.Build();
host.Run();
