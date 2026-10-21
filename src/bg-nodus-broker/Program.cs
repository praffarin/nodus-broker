using System;
using Server.Services;

namespace Server;

/// <summary>
/// Point d'entrée de l'application de gestion du broker MQTT.
/// </summary>
/// <remarks>
/// Cette application sert de lanceur pour le broker MQTT nodus-broker.
/// Elle supporte plusieurs modes d'exécution :
/// - Mode normal : démarre le broker MQTT et continue l'exécution du programme principal
/// - Mode arrêt : arrête toutes les instances du broker en cours (argument --stop)
/// - Mode avec configuration personnalisée : --config ou -c suivi du chemin vers appsettings.json
/// 
/// Le broker est lancé en processus détaché et continue son exécution
/// indépendamment de cette application.
/// 
/// Options de ligne de commande :
/// --stop : arrête toutes les instances du broker MQTT
/// --config ou -c <chemin> : spécifie le fichier de configuration à utiliser
/// </remarks>
internal static class Program {
  /// <summary>
  /// Point d'entrée principal de l'application.
  /// </summary>
  /// <param name="args">
  /// Arguments de ligne de commande.
  /// --stop : arrête toutes les instances du broker MQTT.
  /// --config ou -c <chemin> : chemin vers le fichier de configuration.
  /// </param>
  private static void Main(string[] args) {
    Console.WriteLine("=================================");
    Console.WriteLine("    NODUS-BROKER LAUNCHER");
    Console.WriteLine("=================================");

    // ------------------------------------------------------
    // Parsing des arguments de ligne de commande
    // ------------------------------------------------------

    string? configFilePath = null;

    for (int i = 0; i < args.Length; i++)
    {
      // Commande utilitaire : arrêt manuel du broker
      if (string.Equals(args[i], "--stop", StringComparison.OrdinalIgnoreCase))
      {
        BrokerLauncher.Stop();
        return;
      }

      // Option de configuration personnalisée
      if ((string.Equals(args[i], "--config", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(args[i], "-c", StringComparison.OrdinalIgnoreCase)) &&
          i + 1 < args.Length)
      {
        configFilePath = args[i + 1];
        
        // Vérifier que le fichier existe
        if (!File.Exists(configFilePath))
        {
          Console.WriteLine($"ERREUR : Le fichier de configuration '{configFilePath}' est introuvable.");
          return;
        }
        
        Console.WriteLine($"Configuration personnalisée : {Path.GetFullPath(configFilePath)}");
        i++; // Sauter l'argument suivant (le chemin)
      }
    }

    // ------------------------------------------------------
    // Démarrage du broker MQTT
    // ------------------------------------------------------

    if (!BrokerLauncher.Start(configFilePath))
    {
      Console.WriteLine("ERREUR : impossible de démarrer le broker MQTT.");
      return;
    }

    Console.WriteLine("Broker MQTT démarré avec succès.");

    // ------------------------------------------------------
    // Programme principal
    // ------------------------------------------------------

    // ------------------------------------------------------
    // IMPORTANT :
    //
    // On ne fait PAS :
    //
    // BrokerLauncher.Stop();
    //
    // Le broker MQTT reste donc actif après l'arrêt
    // de cette application. Utiliser
    // "dotnet Server.dll --stop-broker" pour l'arrêter.
    // ------------------------------------------------------

  }
}