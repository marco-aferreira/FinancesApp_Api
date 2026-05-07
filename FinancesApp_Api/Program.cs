using Asp.Versioning;
using FinanceAppDatabase.DbConnection;
using FinancesApp_Api.Jwt;
using FinancesApp_Api.StartUp;
using FinancesApp_Api.SwaggerValues;
using FinancesApp_CQRS;
using FinancesApp_CQRS.Dispatchers;
using FinancesApp_CQRS.EventStore;
using FinancesApp_CQRS.Interfaces;
using FinancesApp_CQRS.Outbox;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .WriteTo.Console(new CommaDelimitedJsonFormatter())
    .CreateBootstrapLogger();

try
{

    var builder = WebApplication.CreateBuilder(args);
    var config = builder.Configuration;

    builder.Host.UseSerilog((ctx, _, loggerConfig) =>
        loggerConfig.ConfigureAppLogging(ctx.Configuration));

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddHealthChecks()
        .AddSqlServer
        (
            connectionString: builder.Configuration.GetConnectionString("DbConnection")!,
            name: "SQL Database Check",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["database", "critical"]
        )
        .AddCheck<S3HealthCheck>("S3 Bucket Check", tags: ["aws", "storage"])
        .AddCheck<DynamoHealthCheck>("DynamoDB Check", tags: ["aws", "storage"]);

    builder.Services.AddJwtServices(config);

    builder.Services.AddScoped<ApiAuthKeyFilter>();
    builder.Services.AddApiVersioning(x =>
    {
        x.DefaultApiVersion = ApiVersions.Current;
        x.AssumeDefaultVersionWhenUnspecified = true;
        x.ReportApiVersions = true;
        x.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new MediaTypeApiVersionReader("api-version"));

    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

    builder.Services.AddSwaggerGen(x =>
    {
        x.OperationFilter<SwaggerDefaultValues>();
    });

    builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
    builder.Services.AddSingleton<ICommandFactory, CommandFactory>();
    builder.Services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
    builder.Services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
    builder.Services.AddSingleton<IEventStore, EventStore>();
    builder.Services.AddSingleton<IEventDispatcher, EventDispatcher>();

    builder.Services.AddHostedService<OutboxProcessor>();

    builder.Services.AddRedis(config);
    builder.Services.AddRateLimiting();

    builder.Services.AddCorsPolicies();

    builder.Services.AddAwsServices(config);

    builder.Services.AddAccountModule();
    builder.Services.AddUserModule(config);
    builder.Services.AddCredentialsModule();

    var app = builder.Build();

    app.AddAccountProjections();
    app.AddUserProjections();
    app.AddCredentialsProjections();

    app.UseHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            var result = JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                duration = report.TotalDuration,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration,
                    tags = e.Value.Tags
                })
            });

            await context.Response.WriteAsync(result);
        }
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
        app.UseHttpsRedirection();

    app.UseCors(CorsInjections.FrontendPolicy);

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseRateLimiter();

    app.MapControllers();

    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
