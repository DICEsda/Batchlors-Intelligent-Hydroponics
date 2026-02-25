using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using IoT.Backend.Models;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using IoT.Backend.WebSockets;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Serilog;

// Allow the MongoDB driver to truncate BSON double values into C# float (Single)
// without throwing TruncationException. This is needed because mongosh and many
// tools store all numbers as 64-bit doubles, while our C# models use float.
// The try-catch guard prevents BsonSerializationException when
// WebApplicationFactory re-enters Program.Main during integration tests.
#pragma warning disable CS0618
try
{
    BsonSerializer.RegisterSerializer(new SingleSerializer(
        MongoDB.Bson.BsonType.Double,
        new MongoDB.Bson.Serialization.Options.RepresentationConverter(allowOverflow: true, allowTruncation: true)));
}
catch (MongoDB.Bson.BsonSerializationException)
{
    // Serializer already registered (e.g. from a previous WebApplicationFactory instance in tests)
}
#pragma warning restore CS0618

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "IoT Backend API", Version = "v1" });
});

// CORS - allow frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:4200" })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// MongoDB
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("MongoDB") 
        ?? "mongodb://localhost:27017";
    return new MongoClient(connectionString);
});

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var databaseName = builder.Configuration["MongoDB:Database"] ?? "iot";
    return client.GetDatabase(databaseName);
});

builder.Services.AddSingleton<IRepository, MongoRepository>();

// Entity-specific repository interfaces (all backed by MongoRepository singleton)
builder.Services.AddSingleton<ICoordinatorRepository>(sp => (MongoRepository)sp.GetRequiredService<IRepository>());
builder.Services.AddSingleton<ITowerRepository>(sp => (MongoRepository)sp.GetRequiredService<IRepository>());
builder.Services.AddSingleton<IOtaJobRepository>(sp => (MongoRepository)sp.GetRequiredService<IRepository>());
builder.Services.AddSingleton<ISettingsRepository>(sp => (MongoRepository)sp.GetRequiredService<IRepository>());
builder.Services.AddSingleton<IZoneRepository>(sp => (MongoRepository)sp.GetRequiredService<IRepository>());
builder.Services.AddSingleton<ITelemetryRepository>(sp => (MongoRepository)sp.GetRequiredService<IRepository>());
builder.Services.AddSingleton<IFirmwareRepository>(sp => (MongoRepository)sp.GetRequiredService<IRepository>());

// Farm and Alert repositories (standalone implementations)
builder.Services.AddSingleton<IFarmRepository, FarmRepository>();
builder.Services.AddSingleton<IAlertRepository, AlertRepository>();

// Digital Twin services
builder.Services.AddSingleton<ITwinRepository, TwinRepository>();
builder.Services.AddSingleton<ITwinService, TwinService>();
builder.Services.AddHostedService<TwinSyncBackgroundService>();

// MQTT
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection(MqttOptions.Section));
builder.Services.AddSingleton<IMqttService, MqttService>();

// WebSocket broadcaster (broadcasts telemetry to all connected clients)
builder.Services.AddSingleton<IWsBroadcaster, WsBroadcaster>();

// Diagnostics service (performance metrics collection and reporting)
builder.Services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
builder.Services.AddHostedService<DiagnosticsPushService>();

// Alert service (monitors telemetry and generates alerts)
builder.Services.AddSingleton<IAlertService, AlertService>();

// Pairing service (manages coordinator-tower pairing workflow)
builder.Services.AddSingleton<IPairingService, PairingService>();
builder.Services.AddHostedService<PairingBackgroundService>();

// Coordinator registration service (manages coordinator onboarding workflow)
builder.Services.AddSingleton<ICoordinatorRegistrationService, CoordinatorRegistrationService>();

// Telemetry handler (processes incoming MQTT and persists to DB)
builder.Services.AddHostedService<TelemetryHandler>();

// WebSocket handler (handles individual client connections and subscriptions)
builder.Services.AddSingleton<IMqttBridgeHandler, MqttBridgeHandler>();

// ML Service (proxies requests to the Python ML FastAPI service)
builder.Services.Configure<MlServiceConfig>(builder.Configuration.GetSection(MlServiceConfig.Section));
builder.Services.AddHttpClient<IMlService, MlService>();

// Azure Digital Twins (optional - only used if configured)
builder.Services.Configure<AzureDigitalTwinsConfig>(builder.Configuration.GetSection(AzureDigitalTwinsConfig.Section));
builder.Services.AddSingleton<IAzureDigitalTwinsService, AzureDigitalTwinsService>();
builder.Services.AddSingleton<AdtTwinMapper>();
builder.Services.AddSingleton<TwinChangeChannel>();
builder.Services.AddHostedService<AdtSyncService>();

// ML Scheduler (periodic ML inference and prediction sync)
builder.Services.Configure<MlSchedulerConfig>(builder.Configuration.GetSection(MlSchedulerConfig.Section));
builder.Services.AddHostedService<MlSchedulerBackgroundService>();

// Health checks
builder.Services.AddHealthChecks()
    .AddMongoDb(
        builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017",
        name: "mongodb",
        timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

// Global error handler – must be first so it catches exceptions from all downstream middleware.
app.UseMiddleware<IoT.Backend.Middleware.ErrorHandlingMiddleware>();

// API key authentication – after error handling so auth failures get proper error formatting.
app.UseMiddleware<IoT.Backend.Middleware.ApiKeyMiddleware>();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();

// WebSocket endpoint
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.Use(async (context, next) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        if (context.Request.Path == "/ws")
        {
            // Subscription-based WebSocket - clients can subscribe to specific topics
            var handler = context.RequestServices.GetRequiredService<IMqttBridgeHandler>();
            var ws = await context.WebSockets.AcceptWebSocketAsync();
            await handler.HandleAsync(ws, context.RequestAborted);
        }
        else if (context.Request.Path == "/ws/broadcast")
        {
            // Broadcast-only WebSocket - receives all telemetry automatically
            var broadcaster = context.RequestServices.GetRequiredService<IWsBroadcaster>();
            var ws = await context.WebSockets.AcceptWebSocketAsync();
            var clientId = broadcaster.RegisterClient(ws);
            
            try
            {
                // Keep connection alive and listen for close
                var buffer = new byte[1024];
                while (ws.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(
                            System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            CancellationToken.None);
                        break;
                    }
                }
            }
            finally
            {
                broadcaster.UnregisterClient(clientId);
            }
        }
        else
        {
            await next();
        }
    }
    else
    {
        await next();
    }
});

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

// Start MQTT service
var mqtt = app.Services.GetRequiredService<IMqttService>();
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStarted.Register(async () =>
{
    try
    {
        await mqtt.StartAsync();
        Log.Information("MQTT service started");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to start MQTT service");
    }
});

lifetime.ApplicationStopping.Register(async () =>
{
    try
    {
        await mqtt.StopAsync();
        Log.Information("MQTT service stopped");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error stopping MQTT service");
    }
});

try
{
    Log.Information("Starting IoT Backend on port 8000");
    app.Run("http://0.0.0.0:8000");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Required for WebApplicationFactory in integration tests
public partial class Program { }
