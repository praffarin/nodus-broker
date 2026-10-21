using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MqttBroker.Configuration;
using MqttBroker.Security;
using System.Reflection;

namespace MqttBroker;

/// <summary>
/// Service applicatif encapsulant le serveur MQTT (MQTTnet).
/// Le MqttServer lui-même est injecté par DI (voir AddMqttServer()
/// dans Program.cs), ce qui permet de partager la même instance
/// entre le transport TCP natif et le transport WebSocket géré
/// par ASP.NET Core / Kestrel.
/// </summary>
public sealed class MqttBrokerService : IAsyncDisposable
{
    private readonly MqttServer _server;
    private readonly ILogger<MqttBrokerService> _logger;
    private readonly AuthOptions _authOptions;
    private readonly BruteForceGuard _bruteForceGuard;
    private readonly TopicAclValidator _aclValidator;

    /*
     * Clients actuellement connectés.
     *
     * La clé est le ClientId.
     */
    private readonly ConcurrentDictionary<string, ClientSession> _connectedClients = new();

    private DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly string _version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion.Split('+')[0] ?? "unknown";

    public MqttBrokerService(
        MqttServer server,
        IOptions<AuthOptions> authOptions,
        IOptions<SecurityOptions> securityOptions,
        TopicAclValidator aclValidator,
        ILogger<MqttBrokerService> logger)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authOptions = authOptions?.Value ?? throw new ArgumentNullException(nameof(authOptions));
        _aclValidator = aclValidator ?? throw new ArgumentNullException(nameof(aclValidator));

        var security = securityOptions?.Value
            ?? throw new ArgumentNullException(nameof(securityOptions));

        _bruteForceGuard = new BruteForceGuard(
            security.MaxFailedAttemptsPerIp,
            security.LockoutDurationSeconds);

        if (_authOptions.IsAnonymous)
        {
            _logger.LogWarning(
                "ATTENTION : le broker MQTT est configuré en mode ANONYME " +
                "(Auth:Mode=\"Anonymous\"). Aucune authentification n'est requise. " +
                "Ne pas utiliser cette configuration en production sans réseau isolé.");
        }

        RegisterEvents();
    }

    private void RegisterEvents()
    {
        _server.ValidatingConnectionAsync += OnValidatingConnectionAsync;
        _server.ClientConnectedAsync += OnClientConnectedAsync;
        _server.ClientDisconnectedAsync += OnClientDisconnectedAsync;
        _server.InterceptingPublishAsync += OnInterceptingPublishAsync;
        _server.InterceptingSubscriptionAsync += OnInterceptingSubscriptionAsync;
    }

    // ============================================================
    // DEMARRAGE
    // ============================================================

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_server.IsStarted)
        {
            return;
        }

        _logger.LogInformation("Démarrage du broker MQTT...");

        _startedAt = DateTimeOffset.UtcNow;

        await _server.StartAsync();

        _logger.LogInformation("Broker MQTT démarré et opérationnel.");
    }

    // ============================================================
    // ARRET
    // ============================================================

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_server.IsStarted)
        {
            return;
        }

        _logger.LogInformation("Arrêt du broker MQTT...");

        await _server.StopAsync();

        _connectedClients.Clear();

        _logger.LogInformation("Broker MQTT arrêté.");
    }

    // ============================================================
    // AUTHENTIFICATION
    // ============================================================

    private Task OnValidatingConnectionAsync(ValidatingConnectionEventArgs e)
    {
        string ip = e.RemoteEndPoint?.ToString() ?? "unknown";

        _logger.LogInformation(
            "Connexion MQTT demandée : ClientId={ClientId}, User={User}, Remote={Remote}",
            e.ClientId, e.UserName, ip);

        /*
         * Vérification du ClientId (toujours requise, même en mode anonyme).
         */

        if (string.IsNullOrWhiteSpace(e.ClientId))
        {
            _logger.LogWarning("Connexion refusée : ClientId vide. IP={Ip}", ip);

            e.ReasonCode = MqttConnectReasonCode.ClientIdentifierNotValid;
            return Task.CompletedTask;
        }

        /*
         * ================================================
         * MODE ANONYME : aucune vérification d'identifiants.
         * ================================================
         */

        if (_authOptions.IsAnonymous)
        {
            e.SessionItems["roles"] = _authOptions.AnonymousRoles;
            e.SessionItems["username"] = e.UserName ?? "anonymous";

            _logger.LogInformation(
                "Connexion acceptée (mode anonyme) : ClientId={ClientId}, Roles={Roles}",
                e.ClientId, string.Join(",", _authOptions.AnonymousRoles));

            e.ReasonCode = MqttConnectReasonCode.Success;
            return Task.CompletedTask;
        }

        /*
         * ================================================
         * MODE CREDENTIALS : authentification obligatoire.
         * ================================================
         */

        if (_bruteForceGuard.IsLocked(ip))
        {
            _logger.LogWarning(
                "Connexion refusée (IP temporairement verrouillée) : IP={Ip}", ip);

            e.ReasonCode = MqttConnectReasonCode.Banned;
            return Task.CompletedTask;
        }

        MqttUser? user = _authOptions.Users
            .FirstOrDefault(u => u.Username == e.UserName);

        bool passwordValid = user is not null
            && !string.IsNullOrEmpty(user.PasswordHash)
            && PasswordHasher.Verify(e.Password ?? string.Empty, user.PasswordHash);

        if (user is null || !passwordValid)
        {
            _bruteForceGuard.RegisterFailure(ip);

            _logger.LogWarning(
                "Authentification refusée : ClientId={ClientId}, User={User}, IP={Ip}",
                e.ClientId, e.UserName, ip);

            e.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            return Task.CompletedTask;
        }

        _bruteForceGuard.RegisterSuccess(ip);

        e.SessionItems["roles"] = user.Roles;
        e.SessionItems["username"] = user.Username;

        _logger.LogInformation(
            "Authentification acceptée : ClientId={ClientId}, User={User}, Roles={Roles}",
            e.ClientId, e.UserName, string.Join(",", user.Roles));

        e.ReasonCode = MqttConnectReasonCode.Success;
        return Task.CompletedTask;
    }

    // ============================================================
    // CLIENT CONNECTE
    // ============================================================

    private async Task OnClientConnectedAsync(ClientConnectedEventArgs e)
    {
        _connectedClients[e.ClientId] = new ClientSession(
            e.ClientId,
            e.UserName ?? string.Empty,
            DateTimeOffset.UtcNow);

        _logger.LogInformation(
            "CLIENT CONNECTE : {ClientId} (clients connectés : {Count})",
            e.ClientId, _connectedClients.Count);

        await PublishSysStatsAsync();
    }

    // ============================================================
    // CLIENT DECONNECTE
    // ============================================================

    private async Task OnClientDisconnectedAsync(ClientDisconnectedEventArgs e)
    {
        _connectedClients.TryRemove(e.ClientId, out _);

        _logger.LogInformation(
            "CLIENT DECONNECTE : {ClientId}, Type={DisconnectType} (clients connectés : {Count})",
            e.ClientId, e.DisconnectType, _connectedClients.Count);

        /*
         * IMPORTANT :
         *
         * On ne fait PAS de reconnexion ici.
         *
         * C'est le CLIENT MQTT qui doit effectuer
         * sa reconnexion.
         */

        await PublishSysStatsAsync();
    }

    // ============================================================
    // PUBLISH (avec contrôle ACL)
    // ============================================================

    private Task OnInterceptingPublishAsync(InterceptingPublishEventArgs e)
    {
        var message = e.ApplicationMessage;

        /*
         * Les messages injectés en interne par le broker lui-même
         * (ex: PublishSysStatsAsync via InjectApplicationMessage)
         * n'ont pas de ClientId : ils ne passent pas par l'ACL,
         * ce ne sont jamais des publications d'un vrai client.
         */
        if (string.IsNullOrEmpty(e.ClientId))
        {
            _logger.LogDebug(
                "PUBLISH interne (broker) : Topic={Topic}", message.Topic);

            return Task.CompletedTask;
        }

        List<string> roles = GetRoles(e.SessionItems);

        if (!_aclValidator.CanPublish(roles, e.ClientId, message.Topic))
        {
            _logger.LogWarning(
                "PUBLISH refusé (ACL) : Client={ClientId}, Topic={Topic}, Roles={Roles}",
                e.ClientId, message.Topic, string.Join(",", roles));

            e.ProcessPublish = false;
            return Task.CompletedTask;
        }

        _logger.LogDebug(
            "PUBLISH : Client={ClientId}, Topic={Topic}, QoS={QoS}, Retain={Retain}",
            e.ClientId, message.Topic, message.QualityOfServiceLevel, message.Retain);

        return Task.CompletedTask;
    }

    // ============================================================
    // SUBSCRIBE (avec contrôle ACL)
    // ============================================================

    private Task OnInterceptingSubscriptionAsync(InterceptingSubscriptionEventArgs e)
    {
        List<string> roles = GetRoles(e.SessionItems);
        string topic = e.TopicFilter.Topic;

        if (!_aclValidator.CanSubscribe(roles, e.ClientId, topic))
        {
            _logger.LogWarning(
                "SUBSCRIBE refusé (ACL) : Client={ClientId}, Topic={Topic}, Roles={Roles}",
                e.ClientId, topic, string.Join(",", roles));

            e.Response.ReasonCode = MqttSubscribeReasonCode.NotAuthorized;
            e.ProcessSubscription = false;
            return Task.CompletedTask;
        }

        _logger.LogDebug(
            "SUBSCRIBE : Client={ClientId}, Topic={Topic}", e.ClientId, topic);

        return Task.CompletedTask;
    }

    // ============================================================
    // PUBLICATION DES STATISTIQUES $SYS / nodus-broker
    // ============================================================

    private async Task PublishSysStatsAsync()
    {
        await PublishInternalAsync(
            "$SYS/nodus-broker/clients/connected",
            _connectedClients.Count.ToString());

        await PublishInternalAsync(
            "$SYS/nodus-broker/version",
            _version);

        await PublishSysStatsUptimeAsync();

        await PublishInternalAsync(
            "$SYS/nodus-broker/clients/list",
            JsonSerializer.Serialize(_connectedClients.Keys.ToList()));
    }

    private async Task PublishInternalAsync(string topic, string payload)
    {
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(true)
                .Build();

            await _server.InjectApplicationMessage(
                new InjectedMqttApplicationMessage(message));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erreur lors de la publication interne sur le topic {Topic}",
                topic);
        }
    }

    // ============================================================
    // UTILITAIRES
    // ============================================================

    private static List<string> GetRoles(System.Collections.IDictionary sessionItems)
    {
        if (sessionItems["roles"] is List<string> roles)
        {
            return roles;
        }

        return new List<string>();
    }

    public async Task PublishSysStatsUptimeAsync()
    {
        TimeSpan uptime = DateTimeOffset.UtcNow - _startedAt;

        await PublishInternalAsync(
            "$SYS/nodus-broker/uptime",
            uptime.ToString(@"dd\:hh\:mm\:ss"));
    }

    /// <summary>
    /// Expose un instantané des clients actuellement connectés
    /// (utile pour un futur endpoint /health ou /status).
    /// </summary>
    public IReadOnlyCollection<ClientSession> GetConnectedClients()
        => _connectedClients.Values.ToList();

    public int ConnectedClientsCount => _connectedClients.Count;

    // ============================================================
    // DISPOSE
    // ============================================================

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        // Le MqttServer est géré par le conteneur DI (AddMqttServer)
        // et sera disposé par celui-ci ; on ne le dispose donc pas
        // ici pour éviter un double-dispose.
    }

    public sealed record ClientSession(string ClientId, string UserName, DateTimeOffset ConnectedAt);
}