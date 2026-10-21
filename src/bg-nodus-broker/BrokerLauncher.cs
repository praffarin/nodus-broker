using System;
using System.IO;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Server.Services;

/// <summary>
/// Service de lancement et d'arrêt du broker MQTT nodus-broker.
/// </summary>
/// <remarks>
/// Gère le cycle de vie du processus broker MQTT embarqué :
/// - Détection d'une instance déjà en cours d'exécution
/// - Démarrage en processus détaché (indépendant de l'application principale)
/// - Vérification de disponibilité du port MQTT (1883)
/// - Arrêt propre par envoi de signal SIGTERM
/// 
/// Le broker est lancé depuis le répertoire de l'application (AppContext.BaseDirectory)
/// et continue son exécution même après l'arrêt de l'application principale.
/// </remarks>
public static class BrokerLauncher
{
  private const string ProcessName = "nodus-broker";

  private const int MqttPort = 1883;
  private static List<string> ProcessNameList = new List<string>() { "nodus-broker" };

    // ============================================================
  // START
  // ============================================================

    /// <summary>
  /// Démarre le broker MQTT si aucune instance n'est déjà en cours d'exécution.
  /// </summary>
  /// <param name="configFilePath">Chemin vers le fichier de configuration (optionnel).</param>
  /// <returns>
  /// true si le broker a été démarré ou si une instance est déjà active, false en cas d'erreur.
  /// </returns>
  /// <remarks>
  /// Le processus est démarré en mode détaché (UseShellExecute=false, CreateNoWindow=true)
  /// et continue son exécution indépendamment de l'application principale.
  /// 
  /// Vérifie d'abord si un broker est déjà en cours d'exécution en testant :
  /// - La présence d'un processus nommé "nodus-broker"
  /// - La disponibilité du port MQTT (1883)
  /// 
  /// Si un chemin de configuration est fourni, il est transmis au broker via l'argument --config.
  /// </remarks>
  public static bool Start(string? configFilePath = null)
  {
    if (IsBrokerAlreadyRunning())
    {
      Console.WriteLine("Broker MQTT déjà en cours d'exécution, on ne relance pas.");

      return true;
    }

    string executableName;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      executableName = $"{ProcessName}.exe";
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
      executableName = ProcessName;
    }
    else
    {
      throw new PlatformNotSupportedException("Système d'exploitation non supporté.");
    }

    string executablePath = Path.Combine(AppContext.BaseDirectory, executableName);

    if (!File.Exists(executablePath))
    {
      Console.WriteLine($"Broker MQTT introuvable : {executablePath}");

      return false;
    }

        var startInfo = new ProcessStartInfo
    {
      FileName = executablePath,
      WorkingDirectory = Path.GetDirectoryName(executablePath)!,

      UseShellExecute = false,
      CreateNoWindow = true,

      RedirectStandardOutput = false,
      RedirectStandardError = false,
      RedirectStandardInput = false
    };

    // Ajouter l'argument --config si un fichier de configuration est spécifié
    if (!string.IsNullOrWhiteSpace(configFilePath))
    {
      startInfo.Arguments = $"--config \"{configFilePath}\"";
      Console.WriteLine($"Démarrage avec configuration : {configFilePath}");
    }

    Process? process = Process.Start(startInfo);

    if (process == null)
    {
      Console.WriteLine("Impossible de démarrer le broker MQTT.");

      return false;
    }

    Console.WriteLine($"Broker MQTT démarré - PID={process.Id}");

    // On ne conserve volontairement pas le Process.
    // Le broker continue son exécution indépendamment
    // de l'application principale.
    process.Dispose();

    return true;
  }

    // ============================================================
  // STOP
  // ============================================================

  /// <summary>
  /// Arrête toutes les instances du broker MQTT en cours d'exécution.
  /// </summary>
  /// <returns>true si au moins un broker a été arrêté, false si aucun n'était actif.</returns>
  /// <remarks>
  /// Envoie un signal SIGTERM à tous les processus nommés "nodus-broker".
  /// Attend jusqu'à 3 secondes pour chaque processus.
  /// Les erreurs d'arrêt sont affichées sur la console mais n'interrompent pas le traitement des autres processus.
  /// </remarks>
  public static bool Stop()
  {
    Process[] processes = Process.GetProcessesByName(ProcessName);

    if (processes.Length == 0)
    {
      Console.WriteLine("Aucun broker MQTT en cours d'exécution.");

      return false;
    }

    foreach (Process p in processes)
    {
      try
      {
        Console.WriteLine($"Arrêt du broker MQTT - PID={p.Id}");

        p.Kill(entireProcessTree: true);
        p.WaitForExit(3000);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Erreur lors de l'arrêt du broker PID={p.Id} : {ex.Message}");
      }
      finally
      {
        p.Dispose();
      }
    }

    return true;
  }

    // ============================================================
  // STATUS
  // ============================================================

  /// <summary>
  /// Vérifie si un broker MQTT est déjà en cours d'exécution et fonctionnel.
  /// </summary>
  /// <returns>true si un broker actif répond sur le port MQTT, false sinon.</returns>
  /// <remarks>
  /// La vérification se fait en deux étapes :
  /// 1. Recherche d'un processus portant le nom "nodus-broker"
  /// 2. Test de connexion TCP sur le port MQTT (1883) pour confirmer que le broker répond
  /// 
  /// Cette double vérification évite les faux positifs liés aux processus zombies
  /// ou aux brokers en cours de démarrage.
  /// </remarks>
  public static bool IsBrokerAlreadyRunning()
  {
    bool isBroker = false;
    foreach (var l_processName in ProcessNameList)
    {
      bool processExists = Process.GetProcessesByName(l_processName).Length > 0;
      if (processExists)
      {
        isBroker = true;
        break;
      }
    }
    if (!isBroker) return false;

    // Le process existe, mais est-il vraiment fonctionnel
    // (écoute-t-il bien sur le port MQTT) ?
    bool isListening = IsPortListening(MqttPort);

    if (!isListening)
    {
      Console.WriteLine("Un process brokerMQTT existe mais ne répond pas " +
                        "sur le port MQTT (process zombie ou en cours de démarrage).");
    }
    return isListening;
  }

  /// <summary>
  /// Teste si un port TCP est à l'écoute sur un hôte donné.
  /// </summary>
  /// <param name="port">Numéro de port à tester.</param>
  /// <param name="host">Adresse de l'hôte. Par défaut : "127.0.0.1".</param>
  /// <param name="timeoutMs">Délai d'attente maximal en millisecondes. Par défaut : 500.</param>
  /// <returns>true si le port est à l'écoute et répond, false sinon.</returns>
  /// <remarks>
  /// Utilise une connexion TCP asynchrone avec timeout pour éviter de bloquer l'application
  /// en cas de port non disponible.
  /// </remarks>
  private static bool IsPortListening(int port, string host = "127.0.0.1", int timeoutMs = 500)
  {
    try
    {
      using var client = new TcpClient();

      IAsyncResult result = client.BeginConnect(host, port, null, null);

      bool success = result.AsyncWaitHandle.WaitOne(timeoutMs);

      if (!success || !client.Connected)
      {
        return false;
      }

      client.EndConnect(result);

      return true;
    }
    catch
    {
      return false;
    }
  }
}