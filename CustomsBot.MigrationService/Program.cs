using CustomsBot.Data;
using CustomsBot.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CustomsBotDbContext>("customsbotdb");
builder.Services.AddHostedService<MigrationWorker>();

var host = builder.Build();
host.Run();
