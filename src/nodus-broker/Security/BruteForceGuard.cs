using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MqttBroker.Security;

/// <summary>
/// Service de protection contre les attaques par force brute sur l'authentification.
/// </summary>
/// <remarks>
/// Fonctionnement :
/// - Suit le nombre de tentatives d'authentification échouées par adresse IP
/// - Verrouille temporairement une IP après dépassement du seuil configuré
/// - Réinitialise le compteur lors d'une authentification réussie
/// - Nettoie périodiquement les entrées expirées (toutes les 5 minutes)
/// 
/// Le verrouillage est basé uniquement sur l'adresse IP distante, indépendamment
/// du nom d'utilisateur ou du ClientId MQTT.
/// </remarks>
public sealed class BruteForceGuard
{
    private sealed class Entry
    {
        public int FailedAttempts;
        public DateTimeOffset? LockedUntil;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly int _maxAttempts;
    private readonly TimeSpan _lockoutDuration;

        /// <summary>
    /// Timer de nettoyage périodique des entrées expirées.
    /// </summary>
    /// <remarks>
    /// Déclenché toutes les 5 minutes pour éviter une fuite mémoire sur un broker
    /// qui tourne longtemps avec beaucoup d'adresses IP distinctes.
    /// </remarks>
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Initialise une nouvelle instance de la classe <see cref="BruteForceGuard"/>.
    /// </summary>
    /// <param name="maxAttempts">Nombre maximum de tentatives échouées avant verrouillage.</param>
    /// <param name="lockoutDurationSeconds">Durée de verrouillage en secondes.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// maxAttempts ou lockoutDurationSeconds est inférieur ou égal à zéro.
    /// </exception>
    public BruteForceGuard(int maxAttempts, int lockoutDurationSeconds)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts),
                "Le nombre maximum de tentatives doit être supérieur à zéro.");
        }

        if (lockoutDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lockoutDurationSeconds),
                "La durée de verrouillage doit être supérieure à zéro.");
        }

        _maxAttempts = maxAttempts;
        _lockoutDuration = TimeSpan.FromSeconds(lockoutDurationSeconds);

        // Nettoyage toutes les 5 minutes.
        _cleanupTimer = new Timer(
            _ => CleanupExpiredEntries(),
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }

        /// <summary>
    /// Indique si l'adresse IP donnée est actuellement verrouillée.
    /// </summary>
    /// <param name="ip">Adresse IP à vérifier.</param>
    /// <returns>true si l'IP est verrouillée, false sinon.</returns>
    /// <remarks>
    /// Si le verrouillage a expiré, l'entrée est automatiquement supprimée et false est retourné.
    /// </remarks>
    public bool IsLocked(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return false;
        }

        if (!_entries.TryGetValue(ip, out Entry? entry))
        {
            return false;
        }

        if (entry.LockedUntil is null)
        {
            return false;
        }

        if (DateTimeOffset.UtcNow >= entry.LockedUntil)
        {
            // Lockout expiré : on retire l'entrée pour repartir à zéro.
            _entries.TryRemove(ip, out _);
            return false;
        }

        return true;
    }

        /// <summary>
    /// Enregistre une tentative d'authentification échouée pour l'adresse IP donnée.
    /// </summary>
    /// <param name="ip">Adresse IP de la tentative échouée.</param>
    /// <remarks>
    /// Si le nombre de tentatives atteint le seuil configuré (maxAttempts),
    /// l'IP est verrouillée pour la durée définie (lockoutDuration).
    /// </remarks>
    public void RegisterFailure(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return;
        }

        Entry entry = _entries.GetOrAdd(ip, static _ => new Entry());

        int attempts = Interlocked.Increment(ref entry.FailedAttempts);

        if (attempts >= _maxAttempts)
        {
            entry.LockedUntil = DateTimeOffset.UtcNow.Add(_lockoutDuration);
        }
    }

        /// <summary>
    /// Réinitialise le compteur d'échecs pour l'adresse IP donnée.
    /// </summary>
    /// <param name="ip">Adresse IP à réinitialiser.</param>
    /// <remarks>
    /// À appeler après une authentification réussie pour supprimer toute entrée existante
    /// et permettre de nouveaux essais.
    /// </remarks>
    public void RegisterSuccess(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return;
        }

        _entries.TryRemove(ip, out _);
    }

        /// <summary>
    /// Nombre d'adresses IP actuellement suivies dans le système.
    /// </summary>
    /// <value>
    /// Inclut les IP verrouillées et celles ayant des tentatives échouées non expirées.
    /// Utile pour le monitoring et l'observabilité.
    /// </value>
    public int TrackedIpCount => _entries.Count;

    private void CleanupExpiredEntries()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (var kvp in _entries)
        {
            Entry entry = kvp.Value;

            bool expired = entry.LockedUntil is not null && now >= entry.LockedUntil;

            if (expired)
            {
                _entries.TryRemove(kvp.Key, out _);
            }
        }
    }
}