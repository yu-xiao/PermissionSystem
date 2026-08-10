using PermissionSystem.Application;
using PermissionSystem.Infrastructure;
using PermissionSystem.Infrastructure.Options;

var builder = Host.CreateApplicationBuilder(args);

var rabbitMqOptions = builder.Configuration
    .GetSection(RabbitMQOptions.SectionName)
    .Get<RabbitMQOptions>() ?? new RabbitMQOptions();

builder.Services.AddApplication(rabbitMqOptions.Enabled && rabbitMqOptions.EnableOutboxPublisher);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.EnvironmentName);
builder.Services.AddHangfireWorker(builder.Configuration);

var host = builder.Build();
host.Run();
