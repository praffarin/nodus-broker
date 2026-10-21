# nodus-cli

Client MQTT en ligne de commande pour interagir avec le broker nodus-broker.

## Installation

```bash
dotnet build src/nodus-cli/nodus-cli.csproj
```

## Utilisation

### Publier un message

```bash
# Publier un message simple
nodus-cli publish -t "test/topic" -m "Hello MQTT"

# Publier avec authentification
nodus-cli publish -t "test/topic" -m "Hello" -u "username" -P "password"

# Publier avec QoS 1 et retain
nodus-cli publish -t "test/topic" -m "Hello" -q 1 -r

# Publier sur un broker distant
nodus-cli publish -h "192.168.1.100" -p 1883 -t "test/topic" -m "Hello"
```

### S'abonner à des topics

```bash
# S'abonner à un topic
nodus-cli subscribe -t "test/topic"

# S'abonner à plusieurs topics
nodus-cli subscribe -t "test/topic1" -t "test/topic2" -t "sensor/#"

# S'abonner avec authentification
nodus-cli subscribe -t "test/#" -u "username" -P "password"

# S'abonner avec QoS 1
nodus-cli subscribe -t "test/#" -q 1
```

## Options communes

- `-h, --host` : Adresse du broker MQTT (défaut: localhost)
- `-p, --port` : Port du broker MQTT (défaut: 1883)
- `-u, --username` : Nom d'utilisateur pour l'authentification
- `-P, --password` : Mot de passe pour l'authentification
- `-i, --client-id` : Identifiant client MQTT (auto-généré par défaut)

## Options de publication

- `-t, --topic` : Topic MQTT (requis)
- `-m, --message` : Message à publier (requis)
- `-q, --qos` : Qualité de service 0, 1 ou 2 (défaut: 0)
- `-r, --retain` : Marquer le message comme retained

## Options d'abonnement

- `-t, --topic` : Topics MQTT (requis, peut être spécifié plusieurs fois)
- `-q, --qos` : Qualité de service 0, 1 ou 2 (défaut: 0)

## Exemples avancés

```bash
# Publier des données de capteur
nodus-cli publish -t "sensor/temperature" -m "23.5"

# S'abonner à tous les capteurs
nodus-cli subscribe -t "sensor/#"

# Publier avec client ID personnalisé
nodus-cli publish -t "device/status" -m "online" -i "device-001"

# S'abonner avec wildcards
nodus-cli subscribe -t "home/+/temperature" -t "home/+/humidity"
```