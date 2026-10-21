namespace MqttBroker;

/// <summary>
/// Service hébergé (<see cref="BackgroundService"/>) chargé de publier
/// périodiquement les statistiques d'uptime du broker MQTT
/// sur le topic système <c>$SYS/nodus-broker/uptime</c>.
/// </summary>
/// <remarks>
/// <para>
/// Ce service s'exécute en tâche de fond dès le démarrage de l'hôte ASP.NET Core
/// (via <c>AddHostedService</c>). Il utilise un <see cref="PeriodicTimer"/>
/// de 60 secondes pour déclencher l'appel à
/// <see cref="MqttBrokerService.PublishSysStatsUptimeAsync"/>.
/// </para>
/// <para>
/// Le <see cref="PeriodicTimer"/> a été choisi plutôt qu'une boucle
/// <c>Task.Delay</c> car il ne dérive pas et respecte proprement
/// l'annulation via <see cref="CancellationToken"/>.
/// </para>
/// <para>
/// Les exceptions levées lors de la publication sont journalisées en
/// <c>Warning</c> et n'interrompent pas la boucle : le service continue
/// de fonctionner même si une publication échoue ponctuellement.
/// </para>
/// <para>
/// L'intervalle de publication (60 s) est défini en constante interne
/// et pourra être rendu configurable dans une version ultérieure.
/// </para>
/// </remarks>
/// <example>
/// Enregistrement dans le pipeline d'injection de dépendances
/// (<c>Program.cs</c>) :
/// <code>
/// builder.Services.AddHostedService&lt;SysStatsUptimePublisher&gt;();
/// </code>
/// </example>
/// <seealso cref="MqttBrokerService"/>
/// <seealso cref="BackgroundService"/>
/// <seealso cref="PeriodicTimer"/>
public sealed class SysStatsUptimePublisher : BackgroundService
{
    /// <summary>
    /// Intervalle entre deux publications de statistiques d'uptime.
    /// </summary>
    /// <remarks>
    /// Valeur fixée à 60 secondes. Une publication trop fréquente
    /// générerait du trafic inutile sur le broker.
    /// </remarks>
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Référence au service broker MQTT utilisé pour publier les messages
    /// système (publication interne, sans client externe).
    /// </summary>
    private readonly MqttBrokerService _broker;

    /// <summary>
    /// Journalise les erreurs de publication et les événements du service.
    /// </summary>
    private readonly ILogger<SysStatsUptimePublisher> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="SysStatsUptimePublisher"/>.
    /// </summary>
    /// <param name="broker">
    /// Instance du service broker MQTT exposant
    /// <see cref="MqttBrokerService.PublishSysStatsUptimeAsync"/>.
    /// Ne doit pas être <see langword="null"/>.
    /// </param>
    /// <param name="logger">
    /// Journaliseur utilisé pour tracer les erreurs de publication.
    /// Ne doit pas être <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Levée si <paramref name="broker"/> ou <paramref name="logger"/>
    /// est <see langword="null"/>.
    /// </exception>
    public SysStatsUptimePublisher(
        MqttBrokerService broker,
        ILogger<SysStatsUptimePublisher> logger)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Boucle principale du service hébergé : publie l'uptime du broker
    /// toutes les <see cref="PublishInterval"/> secondes jusqu'à l'annulation.
    /// </summary>
    /// <param name="stoppingToken">
    /// Jeton d'annulation déclenché par l'hôte lors de l'arrêt de l'application.
    /// Il interrompt immédiatement <see cref="PeriodicTimer.WaitForNextTickAsync"/>
    /// sans attendre la fin de l'intervalle en cours.
    /// </param>
    /// <returns>
    /// Une <see cref="Task"/> représentant l'exécution de la boucle.
    /// La tâche se termine lorsque <paramref name="stoppingToken"/> est annulé.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Les exceptions levées par
    /// <see cref="MqttBrokerService.PublishSysStatsUptimeAsync"/> sont
    /// capturées et journalisées en <c>Warning</c> afin de ne pas
    /// interrompre la boucle de publication.
    /// </para>
    /// <para>
    /// L'annulation via <paramref name="stoppingToken"/> se traduit par
    /// une <see cref="OperationCanceledException"/> levée par
    /// <see cref="PeriodicTimer.WaitForNextTickAsync"/>, propagée
    /// naturellement à l'hôte.
    /// </para>
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PublishInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _broker.PublishSysStatsUptimeAsync();
            }
            catch (Exception ex)
            {
                // Ne pas laisser une exception tuer la boucle
                _logger.LogWarning(
                    ex,
                    "Erreur lors de la publication des statistiques d'uptime $SYS");
            }
        }
    }
}