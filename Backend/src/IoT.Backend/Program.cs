using System.Text.Json;
using System.Text.Json.Serialization;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using IoT.Backend.WebSockets;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MongoDB.Driver;
using Serilog;

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

// MQTT
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection(MqttOptions.Section));
builder.Services.AddSingleton<IMqttService, MqttService>();

// Telemetry handler (processes incoming MQTT and persists to DB)
builder.Services.AddHostedService<TelemetryHandler>();

// WebSocket handler
builder.Services.AddSingleton<MqttBridgeHandler>();

// Health checks
builder.Services.AddHealthChecks()
    .AddMongoDb(
        builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017",
        name: "mongodb",
        timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

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
    if (context.Request.Path == "/ws" && context.WebSockets.IsWebSocketRequest)
    {
        var handler = context.RequestServices.GetRequiredService<MqttBridgeHandler>();
        var ws = await context.WebSockets.AcceptWebSocketAsync();
        await handler.HandleAsync(ws, context.RequestAborted);
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
