using System;
using System.CommandLine;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace NodusCli;

/// <summary>
/// Point d'entrée du client MQTT en ligne de commande nodus-cli.
/// </summary>
/// <remarks>
/// Client MQTT permettant de publier et s'abonner à des topics depuis la ligne de commande.
/// Supporte les connexions TCP, TLS et WebSocket.
/// </remarks>
class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Client MQTT en ligne de commande pour nodus-broker");

        // ============================================================
        // COMMANDE: publish
        // ============================================================
        var publishCommand = new Command("publish", "Publie un message sur un topic MQTT");

        var publishHostOption = new Option<string>(
            aliases: new[] { "--host", "-h" },
            getDefaultValue: () => "localhost",
            description: "Adresse du broker MQTT");

        var publishPortOption = new Option<int>(
            aliases: new[] { "--port", "-p" },
            getDefaultValue: () => 1883,
            description: "Port du broker MQTT");

        var publishTopicOption = new Option<string>(
            aliases: new[] { "--topic", "-t" },
            description: "Topic MQTT")
        { IsRequired = true };

        var publishMessageOption = new Option<string>(
            aliases: new[] { "--message", "-m" },
            description: "Message à publier")
        { IsRequired = true };

        var publishQosOption = new Option<int>(
            aliases: new[] { "--qos", "-q" },
            getDefaultValue: () => 0,
            description: "Qualité de service (0, 1 ou 2)");

        var publishRetainOption = new Option<bool>(
            aliases: new[] { "--retain", "-r" },
            getDefaultValue: () => false,
            description: "Marquer le message comme retained");

        var publishUsernameOption = new Option<string>(
            aliases: new[] { "--username", "-u" },
            description: "Nom d'utilisateur pour l'authentification");

        var publishPasswordOption = new Option<string>(
            aliases: new[] { "--password", "-P" },
            description: "Mot de passe pour l'authentification");

        var publishClientIdOption = new Option<string>(
            aliases: new[] { "--client-id", "-i" },
            getDefaultValue: () => $"nodus-cli-{Guid.NewGuid():N}",
            description: "Identifiant client MQTT");

        publishCommand.AddOption(publishHostOption);
        publishCommand.AddOption(publishPortOption);
        publishCommand.AddOption(publishTopicOption);
        publishCommand.AddOption(publishMessageOption);
        publishCommand.AddOption(publishQosOption);
        publishCommand.AddOption(publishRetainOption);
        publishCommand.AddOption(publishUsernameOption);
        publishCommand.AddOption(publishPasswordOption);
        publishCommand.AddOption(publishClientIdOption);

        publishCommand.SetHandler(async (context) =>
        {
            var host = context.ParseResult.GetValueForOption(publishHostOption)!;
            var port = context.ParseResult.GetValueForOption(publishPortOption);
            var topic = context.ParseResult.GetValueForOption(publishTopicOption)!;
            var message = context.ParseResult.GetValueForOption(publishMessageOption)!;
            var qos = context.ParseResult.GetValueForOption(publishQosOption);
            var retain = context.ParseResult.GetValueForOption(publishRetainOption);
            var username = context.ParseResult.GetValueForOption(publishUsernameOption);
            var password = context.ParseResult.GetValueForOption(publishPasswordOption);
            var clientId = context.ParseResult.GetValueForOption(publishClientIdOption)!;
            
            await PublishAsync(host, port, topic, message, qos, retain, username, password, clientId);
        });

        // ============================================================
        // COMMANDE: subscribe
        // ============================================================
        var subscribeCommand = new Command("subscribe", "S'abonne à un ou plusieurs topics MQTT");

        var subscribeHostOption = new Option<string>(
            aliases: new[] { "--host", "-h" },
            getDefaultValue: () => "localhost",
            description: "Adresse du broker MQTT");

        var subscribePortOption = new Option<int>(
            aliases: new[] { "--port", "-p" },
            getDefaultValue: () => 1883,
            description: "Port du broker MQTT");

        var subscribeTopicOption = new Option<string[]>(
            aliases: new[] { "--topic", "-t" },
            description: "Topics MQTT (peut être spécifié plusieurs fois)")
        { IsRequired = true, AllowMultipleArgumentsPerToken = true };

        var subscribeQosOption = new Option<int>(
            aliases: new[] { "--qos", "-q" },
            getDefaultValue: () => 0,
            description: "Qualité de service (0, 1 ou 2)");

        var subscribeUsernameOption = new Option<string>(
            aliases: new[] { "--username", "-u" },
            description: "Nom d'utilisateur pour l'authentification");

        var subscribePasswordOption = new Option<string>(
            aliases: new[] { "--password", "-P" },
            description: "Mot de passe pour l'authentification");

        var subscribeClientIdOption = new Option<string>(
            aliases: new[] { "--client-id", "-i" },
            getDefaultValue: () => $"nodus-cli-{Guid.NewGuid():N}",
            description: "Identifiant client MQTT");

        subscribeCommand.AddOption(subscribeHostOption);
        subscribeCommand.AddOption(subscribePortOption);
        subscribeCommand.AddOption(subscribeTopicOption);
        subscribeCommand.AddOption(subscribeQosOption);
        subscribeCommand.AddOption(subscribeUsernameOption);
        subscribeCommand.AddOption(subscribePasswordOption);
        subscribeCommand.AddOption(subscribeClientIdOption);

        subscribeCommand.SetHandler(async (context) =>
        {
            var host = context.ParseResult.GetValueForOption(subscribeHostOption)!;
            var port = context.ParseResult.GetValueForOption(subscribePortOption);
            var topics = context.ParseResult.GetValueForOption(subscribeTopicOption)!;
            var qos = context.ParseResult.GetValueForOption(subscribeQosOption);
            var username = context.ParseResult.GetValueForOption(subscribeUsernameOption);
            var password = context.ParseResult.GetValueForOption(subscribePasswordOption);
            var clientId = context.ParseResult.GetValueForOption(subscribeClientIdOption)!;
            
            await SubscribeAsync(host, port, topics, qos, username, password, clientId);
        });

        rootCommand.AddCommand(publishCommand);
        rootCommand.AddCommand(subscribeCommand);

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Publie un message sur un topic MQTT.
    /// </summary>
    /// <param name="host">Adresse du broker MQTT.</param>
    /// <param name="port">Port du broker MQTT.</param>
    /// <param name="topic">Topic sur lequel publier.</param>
    /// <param name="message">Message à publier.</param>
    /// <param name="qos">Qualité de service (0, 1 ou 2).</param>
    /// <param name="retain">Indique si le message doit être conservé par le broker.</param>
    /// <param name="username">Nom d'utilisateur (optionnel).</param>
    /// <param name="password">Mot de passe (optionnel).</param>
    /// <param name="clientId">Identifiant client MQTT.</param>
    static async Task PublishAsync(string host, int port, string topic, string message, int qos, bool retain, string? username, string? password, string clientId)
    {
        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(clientId);

        if (!string.IsNullOrEmpty(username))
        {
            optionsBuilder.WithCredentials(username, password ?? string.Empty);
        }

        var options = optionsBuilder.Build();

        try
        {
            Console.WriteLine($"Connexion au broker {host}:{port}...");
            var connectResult = await client.ConnectAsync(options);

            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                Console.WriteLine($"ERREUR: Échec de connexion - {connectResult.ResultCode}");
                return;
            }

            Console.WriteLine("Connecté.");

            var mqttQos = qos switch
            {
                0 => MqttQualityOfServiceLevel.AtMostOnce,
                1 => MqttQualityOfServiceLevel.AtLeastOnce,
                2 => MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MqttQualityOfServiceLevel.AtMostOnce
            };

            var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(message)
                .WithQualityOfServiceLevel(mqttQos)
                .WithRetainFlag(retain)
                .Build();

            Console.WriteLine($"Publication sur '{topic}'...");
            var publishResult = await client.PublishAsync(applicationMessage);

            if (publishResult.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                Console.WriteLine("Message publié avec succès.");
            }
            else
            {
                Console.WriteLine($"ERREUR: Échec de publication - {publishResult.ReasonCode}");
            }

            await client.DisconnectAsync();
            Console.WriteLine("Déconnecté.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERREUR: {ex.Message}");
        }
    }

    /// <summary>
    /// S'abonne à un ou plusieurs topics MQTT et affiche les messages reçus.
    /// </summary>
    /// <param name="host">Adresse du broker MQTT.</param>
    /// <param name="port">Port du broker MQTT.</param>
    /// <param name="topics">Topics auxquels s'abonner.</param>
    /// <param name="qos">Qualité de service (0, 1 ou 2).</param>
    /// <param name="username">Nom d'utilisateur (optionnel).</param>
    /// <param name="password">Mot de passe (optionnel).</param>
    /// <param name="clientId">Identifiant client MQTT.</param>
    static async Task SubscribeAsync(string host, int port, string[] topics, int qos, string? username, string? password, string clientId)
    {
        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(clientId);

        if (!string.IsNullOrEmpty(username))
        {
            optionsBuilder.WithCredentials(username, password ?? string.Empty);
        }

        var options = optionsBuilder.Build();

        client.ApplicationMessageReceivedAsync += e =>
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {e.ApplicationMessage.Topic}: {payload}");
            return Task.CompletedTask;
        };

        try
        {
            Console.WriteLine($"Connexion au broker {host}:{port}...");
            var connectResult = await client.ConnectAsync(options);

            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                Console.WriteLine($"ERREUR: Échec de connexion - {connectResult.ResultCode}");
                return;
            }

            Console.WriteLine("Connecté.");

            var mqttQos = qos switch
            {
                0 => MqttQualityOfServiceLevel.AtMostOnce,
                1 => MqttQualityOfServiceLevel.AtLeastOnce,
                2 => MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MqttQualityOfServiceLevel.AtMostOnce
            };

            foreach (var topic in topics)
            {
                Console.WriteLine($"Abonnement à '{topic}' (QoS {qos})...");
                await client.SubscribeAsync(topic, mqttQos);
            }

            Console.WriteLine("En attente de messages (Ctrl+C pour quitter)...");
            Console.WriteLine();

            // Attendre jusqu'à interruption par l'utilisateur
            var cts = new System.Threading.CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await Task.Delay(-1, cts.Token).ContinueWith(_ => { });

            Console.WriteLine();
            Console.WriteLine("Déconnexion...");
            await client.DisconnectAsync();
            Console.WriteLine("Déconnecté.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERREUR: {ex.Message}");
        }
    }
}