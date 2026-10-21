

using System.Collections.Generic;
using System;

namespace MqttBroker.Configuration;

// ============================================================
// MQTT — configuration du serveur (endpoints TCP / TLS / WebSocket)
// ============================================================

/// <summary>
/// Options de configuration du serveur MQTT.
/// </summary>
/// <remarks>
/// Configure les endpoints réseau (TCP, TLS, WebSocket), les limites de connexion
/// et les chemins des certificats SSL/TLS.
/// 
/// Mappée depuis la section "Mqtt" de appsettings.json.
/// </remarks>
public sealed class MqttOptions
{
        /// <summary>
    /// Port TCP pour MQTT non chiffré.
    /// </summary>
    /// <value>Par défaut : 1883 (port standard MQTT).</value>
    public int Port { get; set; } = 1883;

    /// <summary>
    /// Port TCP pour MQTT chiffré (TLS).
    /// </summary>
    /// <value>Par défaut : 8883 (port standard MQTT over TLS).</value>
    public int TlsPort { get; set; } = 8883;

    /// <summary>
    /// Active ou désactive le transport MQTT sur TLS.
    /// </summary>
    public bool EnableTls { get; set; }

    /// <summary>
    /// Chemin du fichier certificat SSL/TLS (format .pfx).
    /// </summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// Mot de passe du certificat SSL/TLS.
    /// </summary>
    public string CertificatePassword { get; set; } = string.Empty;

    /// <summary>
    /// Taille du backlog de connexions en attente.
    /// </summary>
    /// <value>Par défaut : 100.</value>
    public int ConnectionBacklog { get; set; } = 100;

    /// <summary>
    /// Nombre maximum de connexions simultanées autorisées.
    /// </summary>
    /// <value>Par défaut : 500.</value>
    public int MaxConnections { get; set; } = 500;

    /// <summary>
    /// Active ou désactive la persistance des sessions MQTT.
    /// </summary>
    /// <remarks>
    /// Lorsque activé, les abonnements et messages QoS 1/2 sont conservés entre les reconnexions.
    /// </remarks>
    /// <value>Par défaut : true.</value>
    public bool PersistentSessions { get; set; } = true;

    /// <summary>
    /// Options de configuration du transport WebSocket.
    /// </summary>
    public WebSocketOptions WebSocket { get; set; } = new();
}

/// <summary>
/// Options de configuration du transport MQTT sur WebSocket.
/// </summary>
public sealed class WebSocketOptions
{
    /// <summary>
    /// Active ou désactive le transport WebSocket.
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// Port HTTP pour les connexions WebSocket non chiffrées.
    /// </summary>
    /// <value>Par défaut : 8080.</value>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Chemin d'endpoint WebSocket.
    /// </summary>
    /// <value>Par défaut : "/mqtt".</value>
    public string Path { get; set; } = "/mqtt";

    /// <summary>
    /// Active ou désactive le transport WebSocket sur TLS (WSS).
    /// </summary>
    public bool EnableTls { get; set; }

    /// <summary>
    /// Port HTTPS pour les connexions WebSocket chiffrées (WSS).
    /// </summary>
    /// <value>Par défaut : 8081.</value>
    public int TlsPort { get; set; } = 8081;
}

// ============================================================
// AUTH — utilisateurs et rôles
// ============================================================

/// <summary>
/// Options de configuration de l'authentification.
/// </summary>
/// <remarks>
/// Supporte deux modes :
/// - "Credentials" : authentification obligatoire par nom d'utilisateur et mot de passe
/// - "Anonymous" : aucune authentification requise, rôles attribués automatiquement
/// 
/// Mappée depuis la section "Auth" de appsettings.json.
/// </remarks>
public sealed class AuthOptions
{
        /// <summary>
    /// Mode d'authentification.
    /// </summary>
    /// <value>
    /// "Credentials" : authentification obligatoire par nom d'utilisateur et mot de passe.
    /// "Anonymous" : aucune authentification requise.
    /// Par défaut : "Anonymous".
    /// </value>
    public string Mode { get; set; } = "Anonymous";

    /// <summary>
    /// Rôles attribués aux clients connectés en mode Anonymous.
    /// </summary>
    /// <remarks>
    /// Utilisés pour évaluer les règles ACL malgré l'absence d'authentification.
    /// </remarks>
    public List<string> AnonymousRoles { get; set; } = new();

    /// <summary>
    /// Liste des utilisateurs autorisés en mode Credentials.
    /// </summary>
    public List<MqttUser> Users { get; set; } = new();

    /// <summary>
    /// Indique si le mode Anonymous est activé.
    /// </summary>
    /// <value>true si Mode="Anonymous", false sinon.</value>
    public bool IsAnonymous =>
        string.Equals(Mode, "Anonymous", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Représente un utilisateur MQTT avec ses identifiants et rôles.
/// </summary>
public sealed class MqttUser
{
    /// <summary>
    /// Nom d'utilisateur.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Hash du mot de passe au format PBKDF2.
    /// </summary>
    /// <remarks>
    /// Format : "{salt_base64}.{hash_base64}".
    /// Généré par l'outil PasswordHashGenerator.
    /// </remarks>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Liste des rôles attribués à cet utilisateur.
    /// </summary>
    /// <remarks>
    /// Les rôles sont utilisés pour évaluer les règles ACL (publish/subscribe).
    /// </remarks>
    public List<string> Roles { get; set; } = new();
}

// ============================================================
// SECURITY — protection anti brute-force
// ============================================================

/// <summary>
/// Options de configuration de la protection anti brute-force.
/// </summary>
/// <remarks>
/// Protège contre les attaques par force brute en limitant le nombre de tentatives
/// d'authentification échouées par adresse IP.
/// 
/// Mappée depuis la section "Security" de appsettings.json.
/// </remarks>
public sealed class SecurityOptions
{
        /// <summary>
    /// Nombre maximum de tentatives d'authentification échouées autorisées par IP.
    /// </summary>
    /// <value>Par défaut : 5.</value>
    public int MaxFailedAttemptsPerIp { get; set; } = 5;

    /// <summary>
    /// Durée de verrouillage d'une IP après dépassement du seuil de tentatives.
    /// </summary>
    /// <value>En secondes. Par défaut : 60.</value>
    public int LockoutDurationSeconds { get; set; } = 60;
}

// ============================================================
// ACL — règles d'autorisation par rôle et par topic
// ============================================================

/// <summary>
/// Règle ACL (Access Control List) définissant les autorisations sur un pattern de topic.
/// </summary>
/// <remarks>
/// Supporte les wildcards MQTT standards :
/// - "+" : correspond à exactement un niveau de topic
/// - "#" : correspond à zéro ou plusieurs niveaux (doit être en fin de pattern)
/// - "{clientid}" : remplacé par le ClientId du client au moment de l'évaluation
/// </remarks>
public sealed class AclRule
{
        /// <summary>
    /// Pattern de topic auquel cette règle s'applique.
    /// </summary>
    /// <remarks>
    /// Peut contenir les wildcards MQTT "+" et "#", ainsi que le placeholder "{clientid}".
    /// </remarks>
    public string TopicPattern { get; set; } = string.Empty;

    /// <summary>
    /// Autorise la publication (PUBLISH) sur les topics correspondant au pattern.
    /// </summary>
    public bool Publish { get; set; }

    /// <summary>
    /// Autorise l'abonnement (SUBSCRIBE) aux topics correspondant au pattern.
    /// </summary>
    public bool Subscribe { get; set; }
}

