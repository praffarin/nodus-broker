using System;
using System.Collections.Generic;
using MqttBroker.Configuration;

namespace MqttBroker.Security;

/// <summary>
/// Valide les droits de publication (PUBLISH) et d'abonnement (SUBSCRIBE) d'un client sur un topic donné.
/// </summary>
/// <remarks>
/// L'évaluation se base sur :
/// - Les rôles attribués au client (issus de l'authentification ou du mode Anonymous)
/// - Les règles ACL configurées dans la section "Acl" de appsettings.json
/// 
/// Format de configuration attendu : Dictionary&lt;string, List&lt;AclRule&gt;&gt;
/// où la clé est le nom du rôle et la valeur la liste des règles ACL applicables.
/// 
/// Les patterns de topics supportent :
/// - Les wildcards MQTT standard ("+" pour un niveau, "#" pour zéro ou plusieurs niveaux)
/// - Le placeholder "{clientid}" remplacé dynamiquement par l'identifiant du client
/// </remarks>
public sealed class TopicAclValidator
{
    private readonly Dictionary<string, List<AclRule>> _rulesByRole;

    public TopicAclValidator(Dictionary<string, List<AclRule>> rulesByRole)
    {
        _rulesByRole = rulesByRole ?? throw new ArgumentNullException(nameof(rulesByRole));
    }

        /// <summary>
    /// Vérifie si l'un des rôles donnés autorise la publication (PUBLISH) sur le topic spécifié.
    /// </summary>
    /// <param name="roles">Rôles attribués au client.</param>
    /// <param name="clientId">Identifiant du client MQTT.</param>
    /// <param name="topic">Topic sur lequel le client souhaite publier.</param>
    /// <returns>true si au moins un rôle autorise la publication, false sinon.</returns>
    public bool CanPublish(IEnumerable<string> roles, string clientId, string topic)
        => Evaluate(roles, clientId, topic, static rule => rule.Publish);

    /// <summary>
    /// Vérifie si l'un des rôles donnés autorise l'abonnement (SUBSCRIBE) au topic spécifié.
    /// </summary>
    /// <param name="roles">Rôles attribués au client.</param>
    /// <param name="clientId">Identifiant du client MQTT.</param>
    /// <param name="topic">Topic auquel le client souhaite s'abonner.</param>
    /// <returns>true si au moins un rôle autorise l'abonnement, false sinon.</returns>
    public bool CanSubscribe(IEnumerable<string> roles, string clientId, string topic)
        => Evaluate(roles, clientId, topic, static rule => rule.Subscribe);

    private bool Evaluate(
        IEnumerable<string> roles,
        string clientId,
        string topic,
        Func<AclRule, bool> permissionSelector)
    {
        if (roles is null || string.IsNullOrEmpty(topic))
        {
            return false;
        }

        foreach (string role in roles)
        {
            if (!_rulesByRole.TryGetValue(role, out List<AclRule>? rules))
            {
                continue;
            }

            foreach (AclRule rule in rules)
            {
                if (!permissionSelector(rule))
                {
                    continue;
                }

                string pattern = rule.TopicPattern.Replace(
                    "{clientid}",
                    clientId,
                    StringComparison.OrdinalIgnoreCase);

                if (MqttTopicMatcher.IsMatch(pattern, topic))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// Implémente la logique de correspondance (matching) de topics MQTT avec support des wildcards.
/// </summary>
/// <remarks>
/// Wildcards supportés :
/// - "+" : correspond à exactement un niveau de topic
/// - "#" : correspond à zéro ou plusieurs niveaux (doit obligatoirement être le dernier segment du pattern)
/// 
/// Règles spéciales :
/// - Les topics système (commençant par "$") ne matchent jamais un wildcard en première position
///   (conformément à la spécification MQTT)
/// </remarks>
internal static class MqttTopicMatcher
{
    /// <summary>
    /// Vérifie si un topic correspond à un pattern donné.
    /// </summary>
    /// <param name="pattern">Pattern contenant éventuellement les wildcards "+" et "#".</param>
    /// <param name="topic">Topic MQTT à tester.</param>
    /// <returns>true si le topic correspond au pattern, false sinon.</returns>
    /// <remarks>
    /// Les topics système (préfixés par "$") ne correspondent jamais à un wildcard placé en début de pattern.
    /// Le wildcard "#" doit être le dernier segment du pattern.
    /// </remarks>
    public static bool IsMatch(string pattern, string topic)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(topic))
        {
            return false;
        }

        // Les topics système ($SYS/...) ne doivent jamais matcher
        // un wildcard # ou + placé en tout début de pattern,
        // conformément à la spec MQTT.
        if (topic.StartsWith('$') && !pattern.StartsWith('$'))
        {
            return false;
        }

        string[] patternSegments = pattern.Split('/');
        string[] topicSegments = topic.Split('/');

        int i = 0;

        for (; i < patternSegments.Length; i++)
        {
            string patternSegment = patternSegments[i];

            if (patternSegment == "#")
            {
                // '#' doit être le dernier segment du pattern
                // et matche tout ce qui suit (y compris zéro niveau).
                return i == patternSegments.Length - 1;
            }

            if (i >= topicSegments.Length)
            {
                // Le topic est plus court que le pattern (hors '#').
                return false;
            }

            if (patternSegment == "+")
            {
                // '+' matche exactement un niveau, quel qu'il soit.
                continue;
            }

            if (!string.Equals(patternSegment, topicSegments[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        // Le pattern est entièrement consommé : le topic doit
        // avoir exactement le même nombre de segments.
        return i == topicSegments.Length;
    }
}