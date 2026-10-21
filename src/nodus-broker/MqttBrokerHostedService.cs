using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MqttBroker;

/// <summary>
/// Service hébergé responsable du cycle de vie applicatif du broker MQTT.
///
/// IMPORTANT :
/// Avec MQTTnet.AspNetCore (AddMqttServer + UseMqttServer dans Program.cs),
/// le démarrage effectif du transport TCP et WebSocket est piloté par le
/// pipeline Kestrel lui-même (dès que l'hôte ASP.NET Core démarre).
///
/// Ce HostedService ne redémarre donc PAS le serveur : il se contente
/// de garantir un arrêt propre (StopAsync) lors d'un SIGTERM / Ctrl+C,
/// afin que les sessions persistées soient fermées correctement plutôt
/// que tuées brutalement.
/// </summary>
public sealed class MqttBrokerHostedService : IHostedService
{
    private readonly MqttBrokerService _broker;
    private readonly ILogger<MqttBrokerHostedService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public MqttBrokerHostedService(
        MqttBrokerService broker,
        IHostApplicationLifetime lifetime,
        ILogger<MqttBrokerHostedService> logger)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ============================================================
    // DEMARRAGE
    // ============================================================

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "MqttBrokerHostedService démarré. Le transport MQTT " +
            "(TCP / WebSocket) est géré par le pipeline Kestrel.");

        // Log une fois que l'hôte est complètement démarré,
        // pour confirmer que le broker répond bien.
        _lifetime.ApplicationStarted.Register(() =>
        {
            _logger.LogInformation(
                "Broker MQTT opérationnel — clients connectés : {Count}",
                _broker.ConnectedClientsCount);
        });

        _lifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogInformation(
                "Arrêt de l'application demandé (SIGTERM/Ctrl+C) — " +
                "fermeture propre du broker MQTT en cours...");
        });

        return Task.CompletedTask;
    }

    // ============================================================
    // ARRET
    // ============================================================

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _broker.StopAsync(cancellationToken);

            _logger.LogInformation(
                "MqttBrokerHostedService arrêté proprement.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erreur lors de l'arrêt propre du broker MQTT.");

            throw;
        }
    }
}