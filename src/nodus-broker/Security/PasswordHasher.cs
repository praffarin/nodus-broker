using System;
using System.Security.Cryptography;

namespace MqttBroker.Security;

/// <summary>
/// Service de hachage et de vérification de mots de passe basé sur PBKDF2.
/// </summary>
/// <remarks>
/// Utilise l'algorithme PBKDF2 (Password-Based Key Derivation Function 2) avec :
/// - Fonction de hachage : SHA-256
/// - Nombre d'itérations : 100 000
/// - Taille du sel : 128 bits (16 octets)
/// - Taille du hash : 256 bits (32 octets)
/// 
/// Format de stockage : "{salt_base64}.{hash_base64}"
/// 
/// Implémentation native .NET via Rfc2898DeriveBytes, sans dépendance externe.
/// La comparaison utilise CryptographicOperations.FixedTimeEquals pour prévenir les attaques par timing.
/// </remarks>
public static class PasswordHasher
{
    private const int SaltSize = 16;   // 128 bits
    private const int HashSize = 32;   // 256 bits
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

        /// <summary>
    /// Génère un hash salé pour le mot de passe donné.
    /// </summary>
    /// <param name="password">Mot de passe en clair à hacher.</param>
    /// <returns>Hash au format "{salt_base64}.{hash_base64}" à stocker dans la configuration.</returns>
    /// <exception cref="ArgumentException">Le mot de passe est vide ou null.</exception>
    /// <remarks>
    /// À utiliser une seule fois pour produire la valeur à stocker dans appsettings.json (section Auth.Users).
    /// Utiliser l'outil PasswordHashGenerator pour générer les hashs de manière interactive.
    /// </remarks>
    public static string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Le mot de passe ne peut pas être vide.", nameof(password));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            Algorithm,
            HashSize);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

        /// <summary>
    /// Vérifie qu'un mot de passe en clair correspond au hash stocké.
    /// </summary>
    /// <param name="password">Mot de passe en clair à vérifier.</param>
    /// <param name="storedHash">Hash stocké au format "{salt_base64}.{hash_base64}".</param>
    /// <returns>true si le mot de passe correspond au hash, false sinon.</returns>
    /// <remarks>
    /// Utilise une comparaison en temps constant (CryptographicOperations.FixedTimeEquals)
    /// pour prévenir les attaques par analyse de temps d'exécution (timing attacks).
    /// 
    /// Retourne false si :
    /// - Le mot de passe ou le hash est vide/null
    /// - Le format du hash est invalide
    /// - Le hash ne correspond pas au mot de passe
    /// </remarks>
    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        string[] parts = storedHash.Split('.');

        if (parts.Length != 2)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[0]);
            expectedHash = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            Algorithm,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}