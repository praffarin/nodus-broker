using PasswordHashGenerator;

/// <summary>
/// Outil en ligne de commande pour générer des hashs de mots de passe PBKDF2.
/// </summary>
/// <remarks>
/// Cet outil permet de générer de manière interactive les hashs de mots de passe
/// au format attendu par le broker MQTT nodus-broker.
/// 
/// Fonctionnalités :
/// - Saisie sécurisée du mot de passe (caractères masqués)
/// - Génération du hash au format "{salt_base64}.{hash_base64}"
/// - Vérification immédiate de la validité du hash généré
/// - Format de sortie directement copiable dans appsettings.json
/// 
/// Utilisation :
///   dotnet run --project tools/PasswordHashGenerator
/// </remarks>
Console.WriteLine("=================================");
Console.WriteLine("   GENERATEUR DE HASH MOT DE PASSE");
Console.WriteLine("=================================");
Console.WriteLine();

while (true)
{
    Console.Write("Nom d'utilisateur (ou vide pour quitter) : ");
    string? username = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(username))
    {
        break;
    }

    Console.Write("Mot de passe : ");
    string password = ReadPasswordMasked();

    if (string.IsNullOrWhiteSpace(password))
    {
        Console.WriteLine("Mot de passe vide, annulé.");
        Console.WriteLine();
        continue;
    }

    string hash = PasswordHasher.Hash(password);

    // Vérification immédiate pour être sûr que le hash généré est valide.
    bool verified = PasswordHasher.Verify(password, hash);

    Console.WriteLine();
    Console.WriteLine($"Vérification : {(verified ? "OK" : "ECHEC (ne devrait jamais arriver)")}");
    Console.WriteLine();
    Console.WriteLine("À coller dans appsettings.json :");
    Console.WriteLine();
    Console.WriteLine("{");
    Console.WriteLine($"  \"Username\": \"{username}\",");
    Console.WriteLine($"  \"PasswordHash\": \"{hash}\",");
    Console.WriteLine("  \"Roles\": [ \"device\" ]");
    Console.WriteLine("}");
    Console.WriteLine();
    Console.WriteLine("-----------------------------------");
    Console.WriteLine();
}

Console.WriteLine("Terminé.");
return;

// ============================================================
// Saisie masquée du mot de passe (évite qu'il s'affiche en clair)
// ============================================================
static string ReadPasswordMasked()
{
    var password = new System.Text.StringBuilder();

    while (true)
    {
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);

        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b");
            }

            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write('*');
        }
    }

    return password.ToString();
}