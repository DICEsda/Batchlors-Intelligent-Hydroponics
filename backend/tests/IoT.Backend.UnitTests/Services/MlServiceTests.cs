using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IoT.Backend.Models;
using IoT.Backend.Models.Ml;
using IoT.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IoT.Backend.UnitTests.Services;

public class MlServiceTests
{
    private readonly MlServiceConfig _config = new()
    {
        BaseUrl = "http://ml-test:8000",
        TimeoutSeconds = 5
    };

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private MlService CreateSut(HttpResponseMessage response)
    {
        var handler = new MockHttpMessageHandler(response);
        var httpClient = new HttpClient(handler);
        return new MlService(
            httpClient,
            Options.Create(_config),
            NullLogger<MlService>.Instance);
    }

    private MlService CreateSut(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> factory)
    {
        var handler = new MockHttpMessageHandler(factory);
        var httpClient = new HttpClient(handler);
        return new MlService(
            httpClient,
            Options.Create(_config),
            NullLogger<MlService>.Instance);
    }

    // ========================================================================
    // PredictGrowthAsync
    // ========================================================================

    [Fact]
    public async Task PredictGrowthAsync_Success_ReturnsResponse()
    {
        var expected = new GrowthPredictionResponse
        {
            TowerId = "tower-1",
            PredictedHeightCm = 25.5,
            CropType = "Lettuce",
            DaysToHarvest = 14,
            GrowthRateCmPerDay = 1.2,
            HealthScore = 0.95,
            Confidence = 0.88,
            ModelName = "growth-v1",
            ModelVersion = "1.0.0",
            GeneratedAt = "2026-02-20T12:00:00Z"
        };

        var sut = CreateSut(MakeJsonResponse(expected));

        var request = MakeGrowthRequest();
        var result = await sut.PredictGrowthAsync(request);

        result.Should().NotBeNull();
        result!.TowerId.Should().Be("tower-1");
        result.PredictedHeightCm.Should().Be(25.5);
    }

    [Fact]
    public async Task PredictGrowthAsync_NonSuccessStatus_ReturnsNull()
    {
        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await sut.PredictGrowthAsync(MakeGrowthRequest());

        result.Should().BeNull();
    }

    [Fact]
    public async Task PredictGrowthAsync_HttpRequestException_ReturnsNull()
    {
        var sut = CreateSut((_, _) => throw new HttpRequestException("Connection refused"));

        var result = await sut.PredictGrowthAsync(MakeGrowthRequest());

        result.Should().BeNull();
    }

    [Fact]
    public async Task PredictGrowthAsync_Timeout_ReturnsNull()
    {
        var sut = CreateSut((_, _) =>
            throw new TaskCanceledException("Timeout", new TimeoutException()));

        var result = await sut.PredictGrowthAsync(MakeGrowthRequest());

        result.Should().BeNull();
    }

    [Fact]
    public async Task PredictGrowthAsync_InvalidJson_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json{{{", Encoding.UTF8, "application/json")
        };
        var sut = CreateSut(response);

        var result = await sut.PredictGrowthAsync(MakeGrowthRequest());

        result.Should().BeNull();
    }

    // ========================================================================
    // DetectAnomalyAsync
    // ========================================================================

    [Fact]
    public async Task DetectAnomalyAsync_Success_ReturnsResponse()
    {
        var expected = new AnomalyDetectionResponse
        {
            TowerId = "tower-1",
            IsAnomalous = true,
            AnomalyScore = 0.87,
            ModelName = "anomaly-v1",
            ModelVersion = "1.0.0",
            GeneratedAt = "2026-02-20T12:00:00Z"
        };

        var sut = CreateSut(MakeJsonResponse(expected));

        var request = MakeAnomalyRequest();
        var result = await sut.DetectAnomalyAsync(request);

        result.Should().NotBeNull();
        result!.IsAnomalous.Should().BeTrue();
        result.AnomalyScore.Should().Be(0.87);
    }

    [Fact]
    public async Task DetectAnomalyAsync_NonSuccessStatus_ReturnsNull()
    {
        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var result = await sut.DetectAnomalyAsync(MakeAnomalyRequest());

        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectAnomalyAsync_HttpRequestException_ReturnsNull()
    {
        var sut = CreateSut((_, _) => throw new HttpRequestException("Network error"));

        var result = await sut.DetectAnomalyAsync(MakeAnomalyRequest());

        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectAnomalyAsync_Timeout_ReturnsNull()
    {
        var sut = CreateSut((_, _) =>
            throw new TaskCanceledException("Timeout", new TimeoutException()));

        var result = await sut.DetectAnomalyAsync(MakeAnomalyRequest());

        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectAnomalyAsync_InvalidJson_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>error</html>", Encoding.UTF8, "application/json")
        };
        var sut = CreateSut(response);

        var result = await sut.DetectAnomalyAsync(MakeAnomalyRequest());

        result.Should().BeNull();
    }

    // ========================================================================
    // GetHealthAsync
    // ========================================================================

    [Fact]
    public async Task GetHealthAsync_Success_ReturnsResponse()
    {
        var expected = new MlHealthResponse
        {
            Status = "healthy",
            UptimeSeconds = 3600,
            Version = "1.0.0",
            MongoDbConnected = true,
            MqttConnected = true,
            ModelsLoaded = new List<string> { "growth-v1", "anomaly-v1" }
        };

        var sut = CreateSut(MakeJsonResponse(expected));

        var result = await sut.GetHealthAsync();

        result.Should().NotBeNull();
        result!.Status.Should().Be("healthy");
        result.UptimeSeconds.Should().Be(3600);
    }

    [Fact]
    public async Task GetHealthAsync_Failure_ReturnsNull()
    {
        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var result = await sut.GetHealthAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetHealthAsync_HttpRequestException_ReturnsNull()
    {
        var sut = CreateSut((_, _) => throw new HttpRequestException("Unreachable"));

        var result = await sut.GetHealthAsync();

        result.Should().BeNull();
    }

    // ========================================================================
    // GetCropsAsync
    // ========================================================================

    [Fact]
    public async Task GetCropsAsync_Success_ReturnsResponse()
    {
        var expected = new CropsResponse
        {
            Count = 2,
            Crops = new List<CropInfo>
            {
                new() { Name = "Lettuce", DaysToHarvest = 45, ExpectedHeightCm = 30 },
                new() { Name = "Basil", DaysToHarvest = 60, ExpectedHeightCm = 40 }
            }
        };

        var sut = CreateSut(MakeJsonResponse(expected));

        var result = await sut.GetCropsAsync();

        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
        result.Crops.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCropsAsync_Failure_ReturnsNull()
    {
        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.GetCropsAsync();

        result.Should().BeNull();
    }

    // ========================================================================
    // GetOptimalConditionsAsync
    // ========================================================================

    [Fact]
    public async Task GetOptimalConditionsAsync_Success_ReturnsResponse()
    {
        var expected = new OptimalConditionsResponse
        {
            CropType = "Lettuce",
            GrowthStage = "vegetative",
            TempMinC = 15,
            TempMaxC = 25,
            TempOptimalC = 20,
            HumidityMinPct = 50,
            HumidityMaxPct = 70,
            HumidityOptimalPct = 60,
            PhMin = 5.5,
            PhMax = 6.5,
            PhOptimal = 6.0,
            EcMinMsCm = 1.0,
            EcMaxMsCm = 2.0,
            EcOptimalMsCm = 1.5,
            LightMinLux = 10000,
            LightMaxLux = 30000,
            LightHoursPerDay = 16,
            ExpectedDaysToHarvest = 45,
            ExpectedHeightCm = 30
        };

        var sut = CreateSut(MakeJsonResponse(expected));

        var result = await sut.GetOptimalConditionsAsync("Lettuce", "vegetative");

        result.Should().NotBeNull();
        result!.CropType.Should().Be("Lettuce");
        result.TempOptimalC.Should().Be(20);
    }

    [Fact]
    public async Task GetOptimalConditionsAsync_Failure_ReturnsNull()
    {
        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.GetOptimalConditionsAsync("Unknown");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOptimalConditionsAsync_VerifyQueryParamsInUrl()
    {
        Uri? capturedUri = null;
        var sut = CreateSut((req, _) =>
        {
            capturedUri = req.RequestUri;
            return Task.FromResult(MakeJsonResponse(new OptimalConditionsResponse
            {
                CropType = "Basil",
                GrowthStage = "flowering"
            }));
        });

        await sut.GetOptimalConditionsAsync("Basil", "flowering");

        capturedUri.Should().NotBeNull();
        capturedUri!.Query.Should().Contain("crop_type=Basil");
        capturedUri.Query.Should().Contain("growth_stage=flowering");
    }

    [Fact]
    public async Task GetOptimalConditionsAsync_DefaultGrowthStage_IsVegetative()
    {
        Uri? capturedUri = null;
        var sut = CreateSut((req, _) =>
        {
            capturedUri = req.RequestUri;
            return Task.FromResult(MakeJsonResponse(new OptimalConditionsResponse()));
        });

        await sut.GetOptimalConditionsAsync("Lettuce");

        capturedUri.Should().NotBeNull();
        capturedUri!.Query.Should().Contain("growth_stage=vegetative");
    }

    [Fact]
    public async Task GetOptimalConditionsAsync_SpecialCharsInCropType_AreEscaped()
    {
        Uri? capturedUri = null;
        var sut = CreateSut((req, _) =>
        {
            capturedUri = req.RequestUri;
            return Task.FromResult(MakeJsonResponse(new OptimalConditionsResponse()));
        });

        await sut.GetOptimalConditionsAsync("Swiss Chard", "seedling");

        capturedUri.Should().NotBeNull();
        capturedUri!.Query.Should().Contain("crop_type=Swiss%20Chard");
    }

    // ========================================================================
    // Constructor / Configuration
    // ========================================================================

    [Fact]
    public void Constructor_SetsBaseAddress()
    {
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);

        _ = new MlService(
            httpClient,
            Options.Create(_config),
            NullLogger<MlService>.Instance);

        httpClient.BaseAddress.Should().Be(new Uri("http://ml-test:8000"));
    }

    [Fact]
    public void Constructor_SetsTimeout()
    {
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);

        _ = new MlService(
            httpClient,
            Options.Create(_config),
            NullLogger<MlService>.Instance);

        httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private HttpResponseMessage MakeJsonResponse<T>(T body)
    {
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static GrowthPredictionRequest MakeGrowthRequest() => new()
    {
        TowerId = "tower-1",
        CropType = "Lettuce",
        CurrentHeightCm = 10.0,
        DaysSincePlanting = 14
    };

    private static AnomalyDetectionRequest MakeAnomalyRequest() => new()
    {
        TowerId = "tower-1",
        Telemetry = new TelemetryInput
        {
            AirTempC = 22.5,
            HumidityPct = 65.0,
            LightLux = 15000
        }
    };
}

/// <summary>
/// Test helper: a mock HttpMessageHandler that returns a fixed response
/// or delegates to a factory function.
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _factory;

    public MockHttpMessageHandler(HttpResponseMessage fixedResponse)
    {
        _factory = (_, _) => Task.FromResult(fixedResponse);
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> factory)
    {
        _factory = factory;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return _factory(request, cancellationToken);
    }
}
