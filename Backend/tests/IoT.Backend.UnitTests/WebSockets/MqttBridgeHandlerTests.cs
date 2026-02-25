using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IoT.Backend.Services;
using IoT.Backend.WebSockets;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IoT.Backend.UnitTests.WebSockets;

/// <summary>
/// Unit tests for <see cref="MqttBridgeHandler"/> WebSocket envelope validation.
/// Verifies that malformed, missing-type, unknown-type, and missing-topic messages
/// are rejected with structured error responses before reaching business logic.
/// </summary>
public class MqttBridgeHandlerTests
{
    private readonly IMqttService _mqtt;
    private readonly MqttBridgeHandler _sut;

    public MqttBridgeHandlerTests()
    {
        _mqtt = Substitute.For<IMqttService>();
        _sut = new MqttBridgeHandler(_mqtt, NullLogger<MqttBridgeHandler>.Instance);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Drives a single text message through the handler and captures every
    /// text frame the handler sends back.
    /// </summary>
    private async Task<List<JsonDocument>> SendAndCollectResponsesAsync(string messageJson)
    {
        var fakeSocket = new FakeWebSocket(messageJson);
        await _sut.HandleAsync(fakeSocket, CancellationToken.None);
        return fakeSocket.SentMessages
            .Select(bytes => JsonDocument.Parse(Encoding.UTF8.GetString(bytes)))
            .ToList();
    }

    private static string Serialize(object obj) =>
        JsonSerializer.Serialize(obj);

    // ── Malformed JSON ─────────────────────────────────────────────────

    [Fact]
    public async Task MalformedJson_ReturnsError()
    {
        var responses = await SendAndCollectResponsesAsync("not json at all {{{");

        responses.Should().ContainSingle();
        var root = responses[0].RootElement;
        root.GetProperty("type").GetString().Should().Be("error");
        root.GetProperty("message").GetString().Should().Contain("malformed JSON");
    }

    // ── Missing 'type' field ───────────────────────────────────────────

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"topic\":\"some/topic\"}")]
    public async Task MissingTypeField_ReturnsError(string json)
    {
        var responses = await SendAndCollectResponsesAsync(json);

        responses.Should().ContainSingle();
        var root = responses[0].RootElement;
        root.GetProperty("type").GetString().Should().Be("error");
        root.GetProperty("message").GetString().Should().Contain("missing 'type' field");
    }

    [Fact]
    public async Task EmptyTypeField_ReturnsError()
    {
        var json = Serialize(new { type = "" });
        var responses = await SendAndCollectResponsesAsync(json);

        responses.Should().ContainSingle();
        var root = responses[0].RootElement;
        root.GetProperty("type").GetString().Should().Be("error");
        root.GetProperty("message").GetString().Should().Contain("missing 'type' field");
    }

    // ── Unknown type ───────────────────────────────────────────────────

    [Theory]
    [InlineData("foobar")]
    [InlineData("CONNECT")]
    [InlineData("heartbeat")]
    public async Task UnknownType_ReturnsError(string type)
    {
        var json = Serialize(new { type });
        var responses = await SendAndCollectResponsesAsync(json);

        responses.Should().ContainSingle();
        var root = responses[0].RootElement;
        root.GetProperty("type").GetString().Should().Be("error");
        root.GetProperty("message").GetString().Should().Contain("unknown type");
    }

    // ── Subscribe / unsubscribe / publish without topic ────────────────

    [Theory]
    [InlineData("subscribe")]
    [InlineData("unsubscribe")]
    [InlineData("publish")]
    public async Task TypeRequiringTopic_WithoutTopic_ReturnsError(string type)
    {
        var json = Serialize(new { type });
        var responses = await SendAndCollectResponsesAsync(json);

        responses.Should().ContainSingle();
        var root = responses[0].RootElement;
        root.GetProperty("type").GetString().Should().Be("error");
        root.GetProperty("message").GetString().Should().Contain("requires a 'topic' field");
    }

    // ── Valid messages pass through ────────────────────────────────────

    [Fact]
    public async Task Ping_ReturnsPong()
    {
        var json = Serialize(new { type = "ping" });
        var responses = await SendAndCollectResponsesAsync(json);

        responses.Should().ContainSingle();
        responses[0].RootElement.GetProperty("type").GetString().Should().Be("pong");
    }

    [Fact]
    public async Task Subscribe_WithTopic_ReturnsSubscribed()
    {
        var json = Serialize(new { type = "subscribe", topic = "hydro/+/telemetry" });
        var responses = await SendAndCollectResponsesAsync(json);

        responses.Should().ContainSingle();
        var root = responses[0].RootElement;
        root.GetProperty("type").GetString().Should().Be("subscribed");
        root.GetProperty("topic").GetString().Should().Be("hydro/+/telemetry");
    }

    [Fact]
    public async Task Unsubscribe_WithTopic_ReturnsUnsubscribed()
    {
        var json = Serialize(new { type = "unsubscribe", topic = "hydro/+/telemetry" });
        var responses = await SendAndCollectResponsesAsync(json);

        responses.Should().ContainSingle();
        var root = responses[0].RootElement;
        root.GetProperty("type").GetString().Should().Be("unsubscribed");
    }

    [Fact]
    public async Task ValidType_CaseInsensitive()
    {
        var json = Serialize(new { type = "PING" });
        var responses = await SendAndCollectResponsesAsync(json);

        responses.Should().ContainSingle();
        responses[0].RootElement.GetProperty("type").GetString().Should().Be("pong");
    }

    [Fact]
    public async Task UnknownType_DoesNotReachMqtt()
    {
        var json = Serialize(new { type = "foobar" });
        await SendAndCollectResponsesAsync(json);

        await _mqtt.DidNotReceive().SubscribeAsync(
            Arg.Any<string>(), Arg.Any<Func<string, byte[], Task>>(),
            Arg.Any<byte>(), Arg.Any<CancellationToken>());
        await _mqtt.DidNotReceive().PublishAsync(
            Arg.Any<string>(), Arg.Any<byte[]>(),
            Arg.Any<byte>(), Arg.Any<CancellationToken>());
    }

    // ── FakeWebSocket ──────────────────────────────────────────────────

    /// <summary>
    /// Minimal <see cref="WebSocket"/> stub that delivers a single text message
    /// on the first <see cref="ReceiveAsync"/> call, then signals Close.
    /// All <see cref="SendAsync"/> payloads are captured for assertions.
    /// </summary>
    private sealed class FakeWebSocket : WebSocket
    {
        private readonly byte[] _incomingBytes;
        private bool _messageDelivered;
        private WebSocketState _state = WebSocketState.Open;

        public List<byte[]> SentMessages { get; } = new();

        public FakeWebSocket(string incomingMessage)
        {
            _incomingBytes = Encoding.UTF8.GetBytes(incomingMessage);
        }

        public override WebSocketState State => _state;
        public override WebSocketCloseStatus? CloseStatus => _state == WebSocketState.Closed
            ? WebSocketCloseStatus.NormalClosure : null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer, CancellationToken ct)
        {
            if (!_messageDelivered)
            {
                _messageDelivered = true;
                Array.Copy(_incomingBytes, 0, buffer.Array!, buffer.Offset,
                    _incomingBytes.Length);
                return Task.FromResult(new WebSocketReceiveResult(
                    _incomingBytes.Length, WebSocketMessageType.Text, endOfMessage: true));
            }

            // Signal close after the single message
            _state = WebSocketState.Closed;
            return Task.FromResult(new WebSocketReceiveResult(
                0, WebSocketMessageType.Close, endOfMessage: true,
                closeStatus: WebSocketCloseStatus.NormalClosure,
                closeStatusDescription: "done"));
        }

        public override Task SendAsync(ArraySegment<byte> buffer,
            WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct)
        {
            SentMessages.Add(buffer.ToArray());
            return Task.CompletedTask;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus,
            string? statusDescription, CancellationToken ct)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus,
            string? statusDescription, CancellationToken ct)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
        }

        public override void Dispose() { }
    }
}
