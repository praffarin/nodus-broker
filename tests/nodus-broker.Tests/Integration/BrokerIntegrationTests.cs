using FluentAssertions;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text;
using Xunit;

namespace MqttBroker.Tests.Integration;

/// <summary>
/// Tests d'intégration du broker MQTT complet.
/// </summary>
/// <remarks>
/// Ces tests nécessitent qu'un broker MQTT soit en cours d'exécution
/// sur localhost:1883 en mode Anonymous.
/// </remarks>
public class BrokerIntegrationTests : IAsyncLifetime
{
    private const string BrokerHost = "localhost";
    private const int BrokerPort = 1883;
    private readonly MqttFactory _factory;
    private IMqttClient? _client;

    public BrokerIntegrationTests()
    {
        _factory = new MqttFactory();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_client != null && _client.IsConnected)
        {
            await _client.DisconnectAsync();
            _client.Dispose();
        }
    }

    [Fact]
    public async Task Connect_WithValidClient_Succeeds()
    {
        // Arrange
        _client = _factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerHost, BrokerPort)
            .WithClientId("test-client-connect")
            .Build();

        // Act
        var result = await _client.ConnectAsync(options);

        // Assert
        result.ResultCode.Should().Be(MqttClientConnectResultCode.Success);
        _client.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task Publish_WithQos0_Succeeds()
    {
        // Arrange
        _client = _factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerHost, BrokerPort)
            .WithClientId("test-client-publish-qos0")
            .Build();

        await _client.ConnectAsync(options);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic("test/qos0")
            .WithPayload("Hello QoS 0")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        // Act
        var result = await _client.PublishAsync(message);

        // Assert
        result.ReasonCode.Should().Be(MqttClientPublishReasonCode.Success);
    }

    [Fact]
    public async Task Publish_WithQos1_Succeeds()
    {
        // Arrange
        _client = _factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerHost, BrokerPort)
            .WithClientId("test-client-publish-qos1")
            .Build();

        await _client.ConnectAsync(options);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic("test/qos1")
            .WithPayload("Hello QoS 1")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        // Act
        var result = await _client.PublishAsync(message);

        // Assert
        result.ReasonCode.Should().Be(MqttClientPublishReasonCode.Success);
    }

    [Fact]
    public async Task Publish_WithQos2_Succeeds()
    {
        // Arrange
        _client = _factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerHost, BrokerPort)
            .WithClientId("test-client-publish-qos2")
            .Build();

        await _client.ConnectAsync(options);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic("test/qos2")
            .WithPayload("Hello QoS 2")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        // Act
        var result = await _client.PublishAsync(message);

        // Assert
        result.ReasonCode.Should().Be(MqttClientPublishReasonCode.Success);
    }

    [Fact]
    public async Task Subscribe_AndReceiveMessage_Succeeds()
    {
        // Arrange
        var subscriber = _factory.CreateMqttClient();
        var publisher = _factory.CreateMqttClient();

        var subscriberOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerHost, BrokerPort)
            .WithClientId("test-subscriber")
            .Build();

        var publisherOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerHost, BrokerPort)
            .WithClientId("test-publisher")
            .Build();

        string? receivedTopic = null;
        string? receivedPayload = null;
        var messageReceived = new TaskCompletionSource<bool>();

        subscriber.ApplicationMessageReceivedAsync += e =>
        {
            receivedTopic = e.ApplicationMessage.Topic;
            receivedPayload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            messageReceived.SetResult(true);
            return Task.CompletedTask;
        };

        await subscriber.ConnectAsync(subscriberOptions);
        await publisher.ConnectAsync(publisherOptions);

        // Act
        await subscriber.SubscribeAsync("test/subscribe");
        
        await Task.Delay(100); // Laisser le temps à l'abonnement de s'établir

        var message = new MqttApplicationMessageBuilder()
            .WithTopic("test/subscribe")
            .WithPayload("Test Message")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await publisher.PublishAsync(message);

        // Attendre la réception du message (max 5 secondes)
        var received = await Task.WhenAny(messageReceived.Task, Task.Delay(5000)) == messageReceived.Task;

        // Assert
        received.Should().BeTrue("le message aurait dû être reçu");
        receivedTopic.Should().Be("test/subscribe");
        receivedPayload.Should().Be("Test Message");

        // Cleanup
        await subscriber.DisconnectAsync();
        await publisher.DisconnectAsync();
        subscriber.Dispose();
        publisher.Dispose();
    }

    [Fact]
    public async Task Subscribe_WithWildcardHash_ReceivesMessages()
    {
        // Arrange
        var subscriber = _factory.CreateMqttClient();
        var publisher = _factory.CreateMqttClient();

        var subscriberOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerHost, BrokerPort)
            .WithClientId("test-subscriber-wildcard")
            .Build();

        var publisherOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerHost, BrokerPort)
            .WithClientId("test-publisher-wildcard")
            .Build();

        var receivedMessages = new List<string>();
        var messageCount = new TaskCompletionSource<bool>();

        subscriber.ApplicationMessageReceivedAsync += e =>
        {
            receivedMessages.Add(e.ApplicationMessage.Topic);
            if (receivedMessages.Count >= 2)
            {
                messageCount.SetResult(true);
            }
            return Task.CompletedTask;
        };

        await subscriber.ConnectAsync(subscriberOptions);
        await publisher.ConnectAsync(publisherOptions);

        // Act
        await subscriber.SubscribeAsync("sensor/#");
        await Task.Delay(100);

        await publisher.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic("sensor/temperature")
            .WithPayload("23.5")
            .Build());

        await publisher.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic("sensor/humidity")
            .WithPayload("60")
            .Build());

        var received = await Task.WhenAny(messageCount.Task, Task.Delay(5000)) == messageCount.Task;

        // Assert
        received.Should().BeTrue();
        receivedMessages.Should().Contain("sensor/temperature");
        receivedMessages.Should().Contain("sensor/humidity");

        // Cleanup
        await subscriber.DisconnectAsync();
        await publisher.DisconnectAsync();
        subscriber.Dispose();
        publisher.Dispose();
    }

    [Fact]
    public async Task RetainedMessage_IsReceivedByNewSubscriber()
    {
        // Arrange
        var publisher = _factory.CreateMqttClient();
        var subscriber = _factory.CreateMqttClient();

        var publisherOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerHost, BrokerPort)
            .WithClientId("test-publisher-retain")
            .Build();

        var subscriberOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerHost, BrokerPort)
            .WithClientId("test-subscriber-retain")
            .Build();

        string? receivedPayload = null;
        var messageReceived = new TaskCompletionSource<bool>();

        subscriber.ApplicationMessageReceivedAsync += e =>
        {
            receivedPayload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            messageReceived.SetResult(true);
            return Task.CompletedTask;
        };

        await publisher.ConnectAsync(publisherOptions);

        // Act - Publier un message retained
        var retainedMessage = new MqttApplicationMessageBuilder()
            .WithTopic("test/retained")
            .WithPayload("Retained Message")
            .WithRetainFlag(true)
            .Build();

        await publisher.PublishAsync(retainedMessage);
        await publisher.DisconnectAsync();

        // S'abonner après la publication
        await Task.Delay(200);
        await subscriber.ConnectAsync(subscriberOptions);
        await subscriber.SubscribeAsync("test/retained");

        var received = await Task.WhenAny(messageReceived.Task, Task.Delay(5000)) == messageReceived.Task;

        // Assert
        received.Should().BeTrue("le message retained aurait dû être reçu immédiatement après l'abonnement");
        receivedPayload.Should().Be("Retained Message");

        // Cleanup - Supprimer le message retained
        await publisher.ConnectAsync(publisherOptions);
        await publisher.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic("test/retained")
            .WithPayload(Array.Empty<byte>())
            .WithRetainFlag(true)
            .Build());

        await subscriber.DisconnectAsync();
        await publisher.DisconnectAsync();
        subscriber.Dispose();
        publisher.Dispose();
    }

    [Fact]
    public async Task MultipleClients_CanConnectSimultaneously()
    {
        // Arrange
        var clients = new List<IMqttClient>();
        const int clientCount = 10;

        // Act
        for (int i = 0; i < clientCount; i++)
        {
            var client = _factory.CreateMqttClient();
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(BrokerHost, BrokerPort)
                .WithClientId($"test-client-{i}")
                .Build();

            var result = await client.ConnectAsync(options);
            result.ResultCode.Should().Be(MqttClientConnectResultCode.Success);
            clients.Add(client);
        }

        // Assert
        clients.Should().HaveCount(clientCount);
        clients.Should().OnlyContain(c => c.IsConnected);

        // Cleanup
        foreach (var client in clients)
        {
            await client.DisconnectAsync();
            client.Dispose();
        }
    }
}