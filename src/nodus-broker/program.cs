using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet.AspNetCore;
using MqttBroker;
using MqttBroker.Configuration;
using MqttBroker.Security;


/// <summary>
/// Point d'entrée principal du broker MQTT nodus-broker.
/// Configure et démarre un serveur MQTT compatible avec les transports TCP natif, TLS et WebSocket.
/// </summary>
/// <remarks>
/// Architecture :
/// - Utilise MQTTnet.AspNetCore pour intégrer le serveur MQTT dans le pipeline ASP.NET Core
/// - Supporte trois modes de transport : TCP (1883), TLS (8883), WebSocket (8080/8081)
/// - Configuration centralisée via appsettings.json (paramétrable en ligne de commande)
/// - Système d'authentification avec modes Credentials ou Anonymous
/// - Contrôle d'accès ACL (Access Control List) par rôle et par topic
/// - Protection anti brute-force sur les tentatives d'authentification
/// 
/// Le serveur partage une seule instance MqttServer entre tous les transports,
/// permettant aux clients connectés via TCP, TLS ou WebSocket de communiquer entre eux.
/// 
/// Options de ligne de commande :
/// --config ou -c : Chemin vers le fichier de configuration (défaut: appsettings.json)
/// </remarks>

// Parsing des arguments de ligne de commande
string configFilePath = "appsettings.json";
var cleanedArgs = new List<string>();

for (int i = 0; i < args.Length; i++)
{
    if ((args[i] == "--config" || args[i] == "-c") && i + 1 < args.Length)
    {
        configFilePath = args[i + 1];
        i++; // Sauter l'argument suivant (le chemin)
        // Ne pas ajouter --config et son chemin aux arguments nettoyés
    }
    else
    {
        cleanedArgs.Add(args[i]);
    }
}

var builder = WebApplication.CreateBuilder(cleanedArgs.ToArray());

// ============================================================
// CONFIGURATION
// ============================================================

// Charger le fichier de configuration spécifié
if (!File.Exists(configFilePath))
{
    Console.WriteLine($"ERREUR: Le fichier de configuration '{configFilePath}' est introuvable.");
    Environment.Exit(1);
}

builder.Configuration.AddJsonFile(configFilePath, optional: false, reloadOnChange: true);
Console.WriteLine($"Configuration chargée depuis: {Path.GetFullPath(configFilePath)}");

var mqttSection = builder.Configuration.GetSection("Mqtt");
builder.Services.Configure<MqttOptions>(mqttSection);
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));

var mqttOptions = mqttSection.Get<MqttOptions>() ?? new MqttOptions();

builder.Services.AddSingleton<TopicAclValidator>(sp =>
{
    var config = builder.Configuration.GetSection("Acl")
        .Get<Dictionary<string, List<AclRule>>>()
        ?? new Dictionary<string, List<AclRule>>();

    return new TopicAclValidator(config);
});

// ============================================================
// MQTT + ASP.NET CORE (TCP natif + WebSocket)
// ============================================================

builder.Services
    .AddMqttServer(mqttServerOptionsBuilder =>
    {
        mqttServerOptionsBuilder
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(mqttOptions.Port)
            .WithConnectionBacklog(mqttOptions.ConnectionBacklog)
            .WithPersistentSessions(mqttOptions.PersistentSessions);
    })
    .AddMqttConnectionHandler()
    .AddConnections();

builder.Services.AddSingleton<MqttBrokerService>();
builder.Services.AddHostedService<MqttBrokerHostedService>();
builder.Services.AddHostedService<SysStatsUptimePublisher>();

// ============================================================
// KESTREL — ports TCP / TLS / WebSocket
// ============================================================

builder.WebHost.ConfigureKestrel(kestrel =>
{
    // Port TCP MQTT natif
    kestrel.ListenAnyIP(mqttOptions.Port, l => l.UseMqtt());

    // Port TCP MQTT chiffré (TLS)
    if (mqttOptions.EnableTls)
    {
        kestrel.ListenAnyIP(mqttOptions.TlsPort, l =>
        {
            l.UseHttps(mqttOptions.CertificatePath, mqttOptions.CertificatePassword);
            l.UseMqtt();
        });
    }

    // Port WebSocket (HTTP simple, upgrade géré par UseWebSockets)
    if (mqttOptions.WebSocket.Enable)
    {
        kestrel.ListenAnyIP(mqttOptions.WebSocket.Port);

        if (mqttOptions.WebSocket.EnableTls)
        {
            kestrel.ListenAnyIP(mqttOptions.WebSocket.TlsPort, l =>
                l.UseHttps(mqttOptions.CertificatePath, mqttOptions.CertificatePassword));
        }
    }
});

var app = builder.Build();

// ============================================================
// PIPELINE HTTP / WEBSOCKET
// ============================================================

if (mqttOptions.WebSocket.Enable)
{
    app.UseWebSockets();

    // Enregistrement top-level (pas de UseEndpoints requis).
    app.MapConnectionHandler<MqttConnectionHandler>(
        mqttOptions.WebSocket.Path,
        httpConnectionDispatcherOptions =>
            httpConnectionDispatcherOptions.WebSockets.SubProtocolSelector =
                protocolList => protocolList.FirstOrDefault() ?? string.Empty);
}

// Câble les handlers (auth, ACL, logging) définis dans MqttBrokerService
// sur le MqttServer partagé entre TCP et WebSocket.
app.UseMqttServer(server =>
{
    // Rien à faire ici : MqttBrokerService s'abonne déjà aux
    // événements du MqttServer injecté via son constructeur.
});

await app.RunAsync();