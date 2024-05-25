using System.Reflection;

using JobService.Components;
using JobService.Service;
using JobService.Service.Components;

using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;

using Microsoft.EntityFrameworkCore;

using Serilog;
using Serilog.Events;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

string? optionsEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
bool useOtlpExporter = !string.IsNullOrWhiteSpace(optionsEndpoint);
LoggerConfiguration logBuilder = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("MassTransit", LogEventLevel.Debug)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console();
if (useOtlpExporter)
{
    logBuilder.WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        AddHeaders(options.Headers, builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"]);
        AddResourceAttributes(options.ResourceAttributes, builder.Configuration["OTEL_RESOURCE_ATTRIBUTES"]);

        options.ResourceAttributes.Add("service.name", "apiservice");
        return;

        void AddResourceAttributes(IDictionary<string, object> attributes, string attributeConfig)
        {
            if (!string.IsNullOrEmpty(attributeConfig))
            {
                string[] parts = attributeConfig.Split('=');
                if (parts.Length == 2)
                {
                    attributes[parts[0]] = parts[1];
                }
                else
                {
                    throw new InvalidOperationException($"Invalid resource attribute format: {attributeConfig}");
                }
            }
        }

        void AddHeaders(IDictionary<string, string> headers, string headerConfig)
        {
            if (!string.IsNullOrEmpty(headerConfig))
            {
                foreach (string header in headerConfig.Split(','))
                {
                    string[] parts = header.Split('=');
                    if (parts.Length == 2)
                    {
                        headers[parts[0]] = parts[1];
                    }
                    else
                    {
                        throw new InvalidOperationException($"Invalid header format: {header}");
                    }
                }
            }
        }
    });
}

Log.Logger = logBuilder.CreateBootstrapLogger();
builder.Logging.ClearProviders().AddSerilog();
builder.Host.UseSerilog();
Log.Logger.Information("Configured Serilog to send to: {Endpoint}", optionsEndpoint);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddDbContext<JobServiceSagaDbContext>(optionsBuilder =>
{
    string? connectionString = builder.Configuration.GetConnectionString("JobService");
    optionsBuilder.UseNpgsql(connectionString, m =>
    {
        m.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
        m.MigrationsHistoryTable($"__{nameof(JobServiceSagaDbContext)}");
    });
});
builder.Services.AddHostedService<MigrationHostedService<JobServiceSagaDbContext>>();

builder.Services.AddMassTransit(x =>
{
    x.AddDelayedMessageScheduler();

    x.AddConsumer<ConvertVideoJobConsumer, ConvertVideoJobConsumerDefinition>()
        .Endpoint(e => e.Name = "convert-job-queue");

    x.AddConsumer<TrackVideoConvertedConsumer>();

    x.SetJobConsumerOptions();
    x.AddJobSagaStateMachines(options => options.FinalizeCompleted = false)
        .EntityFrameworkRepository(r =>
        {
            r.ExistingDbContext<JobServiceSagaDbContext>();
            r.UsePostgres();
        });

    x.SetKebabCaseEndpointNameFormatter();

    x.UsingRabbitMq((context, cfg) =>
    {
        var connectionString = context.GetRequiredService<IConfiguration>()
            .GetConnectionString("RabbitMq")!;
        cfg.Host(new Uri(connectionString), _ => {});
        cfg.UseDelayedMessageScheduler();
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddOptions<MassTransitHostOptions>()
    .Configure(options =>
    {
        options.WaitUntilStarted = true;
        options.StartTimeout = TimeSpan.FromMinutes(1);
        options.StopTimeout = TimeSpan.FromMinutes(1);
    });

builder.Services.AddOptions<HostOptions>()
    .Configure(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromMinutes(2);
    });



WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
await app.RunAsync();