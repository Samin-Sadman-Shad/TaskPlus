using SmartTask.Api.Extensions;
using SmartTask.Application;
using SmartTask.Infrastructure;
using SmartTask.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddPersistence(builder.Configuration)
    .AddInfrastructure(builder.Configuration)
    .AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseApiPipeline();

app.Run();

/// <summary>Exposed so integration tests can host the API with <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
