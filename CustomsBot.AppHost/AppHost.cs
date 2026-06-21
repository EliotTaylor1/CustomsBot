var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var customsbotdb = postgres.AddDatabase("customsbotdb");

var migrations = builder.AddProject<Projects.CustomsBot_MigrationService>("migrations")
    .WithReference(customsbotdb)
    .WaitFor(customsbotdb);

var discordToken = builder.AddParameter("discord-token", secret: true);
var riotApiKey = builder.AddParameter("riot-api-key", secret: true);
var discordClientId = builder.AddParameter("discord-client-id");
var discordClientSecret = builder.AddParameter("discord-client-secret", secret: true);

var bot = builder.AddProject<Projects.CustomsBot_Bot>("bot")
    .WithHttpEndpoint()
    .WithReference(customsbotdb)
    .WaitFor(customsbotdb)
    .WaitForCompletion(migrations)
    .WithEnvironment("Discord__Token", discordToken)
    .WithEnvironment("Riot__ApiKey", riotApiKey);

var server = builder.AddProject<Projects.CustomsBot_Server>("server")
    .WithHttpHealthCheck("/health")
    .WithReference(customsbotdb)
    .WithReference(bot)
    .WaitFor(customsbotdb)
    .WaitForCompletion(migrations)
    .WithEnvironment("Riot__ApiKey", riotApiKey)
    .WithEnvironment("Discord__ClientId", discordClientId)
    .WithEnvironment("Discord__ClientSecret", discordClientSecret)
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
