using PermissionSystem.Application;
using PermissionSystem.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHangfireWorker(builder.Configuration);

var host = builder.Build();
host.Run();
