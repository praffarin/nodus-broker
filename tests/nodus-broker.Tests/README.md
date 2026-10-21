# Tests nodus-broker

Suite de tests automatisés pour le broker MQTT nodus-broker.

## Structure des tests

```
tests/nodus-broker.Tests/
├── Security/
│   ├── PasswordHasherTests.cs        # Tests du hachage de mots de passe
│   ├── BruteForceGuardTests.cs       # Tests de la protection anti brute-force
│   └── TopicAclValidatorTests.cs     # Tests des règles ACL
├── Integration/
│   └── BrokerIntegrationTests.cs     # Tests d'intégration du broker complet
└── README.md
```

## Prérequis

- .NET 9.0 SDK
- Un broker MQTT en cours d'exécution sur localhost:1883 (pour les tests d'intégration)

## Exécution des tests

### Tous les tests

```bash
dotnet test tests/nodus-broker.Tests/nodus-broker.Tests.csproj
```

### Tests unitaires uniquement (pas besoin de broker)

```bash
dotnet test tests/nodus-broker.Tests/nodus-broker.Tests.csproj --filter "FullyQualifiedName~Security"
```

### Tests d'intégration uniquement (nécessite un broker actif)

```bash
# Démarrer d'abord le broker
cd src/nodus-broker
dotnet run

# Dans un autre terminal, exécuter les tests d'intégration
dotnet test tests/nodus-broker.Tests/nodus-broker.Tests.csproj --filter "FullyQualifiedName~Integration"
```

### Avec couverture de code

```bash
dotnet test tests/nodus-broker.Tests/nodus-broker.Tests.csproj --collect:"XPlat Code Coverage"
```

## Tests unitaires

### PasswordHasherTests

Tests du service de hachage PBKDF2 :
- ✓ Génération de hash au format correct
- ✓ Unicité des hashs (sels aléatoires)
- ✓ Vérification de mots de passe valides/invalides
- ✓ Sensibilité à la casse
- ✓ Gestion des formats invalides
- ✓ Gestion des entrées nulles/vides

### BruteForceGuardTests

Tests de la protection anti brute-force :
- ✓ Verrouillage après N tentatives échouées
- ✓ Réinitialisation après authentification réussie
- ✓ Expiration automatique du verrouillage
- ✓ Suivi indépendant par IP
- ✓ Compteur d'IP suivies
- ✓ Gestion des entrées nulles/vides

### TopicAclValidatorTests

Tests de validation des règles ACL :
- ✓ Correspondance exacte de topics
- ✓ Wildcards MQTT (+ et #)
- ✓ Placeholder {clientid}
- ✓ Permissions publish/subscribe séparées
- ✓ Multiples rôles par client
- ✓ Protection des topics système ($SYS)
- ✓ Gestion des rôles inconnus

## Tests d'intégration

### BrokerIntegrationTests

Tests du broker complet avec clients réels :
- ✓ Connexion de clients MQTT
- ✓ Publication avec QoS 0, 1 et 2
- ✓ Abonnement et réception de messages
- ✓ Wildcards dans les abonnements (# et +)
- ✓ Messages retained
- ✓ Connexions simultanées multiples

**Important** : Ces tests nécessitent un broker actif sur localhost:1883 en mode Anonymous.

### Configuration du broker pour les tests

Dans `appsettings.json` :

```json
{
  "Auth": {
    "Mode": "Anonymous",
    "AnonymousRoles": ["device", "admin"]
  },
  "Mqtt": {
    "Port": 1883
  }
}
```

## Exécution en CI/CD

```yaml
# Exemple GitHub Actions
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Tests unitaires
        run: dotnet test tests/nodus-broker.Tests/nodus-broker.Tests.csproj --filter "FullyQualifiedName~Security"
      
      - name: Démarrer le broker
        run: |
          cd src/nodus-broker
          dotnet run &
          sleep 5
      
      - name: Tests d'intégration
        run: dotnet test tests/nodus-broker.Tests/nodus-broker.Tests.csproj --filter "FullyQualifiedName~Integration"
```

## Dépendances de test

- **xUnit** : Framework de tests
- **FluentAssertions** : Assertions fluides et lisibles
- **MQTTnet** : Client MQTT pour les tests d'intégration
- **coverlet.collector** : Couverture de code

## Ajout de nouveaux tests

### Test unitaire

```csharp
[Fact]
public void MyMethod_WithValidInput_ReturnsExpectedResult()
{
    // Arrange
    var sut = new MyClass();
    
    // Act
    var result = sut.MyMethod("input");
    
    // Assert
    result.Should().Be("expected");
}
```

### Test d'intégration

```csharp
[Fact]
public async Task MyScenario_Succeeds()
{
    // Arrange
    var client = _factory.CreateMqttClient();
    var options = new MqttClientOptionsBuilder()
        .WithTcpServer(BrokerHost, BrokerPort)
        .Build();
    
    // Act
    await client.ConnectAsync(options);
    // ... test scenario
    
    // Assert
    // ... vérifications
    
    // Cleanup
    await client.DisconnectAsync();
    client.Dispose();
}
```

## Bonnes pratiques

1. **Noms de tests descriptifs** : `MethodName_Scenario_ExpectedResult`
2. **AAA Pattern** : Arrange, Act, Assert
3. **Tests isolés** : Chaque test doit être indépendant
4. **Cleanup systématique** : Libérer les ressources (connexions, clients)
5. **Tests paramétrés** : Utiliser `[Theory]` et `[InlineData]` pour éviter la duplication
6. **Assertions claires** : Utiliser FluentAssertions pour des messages d'erreur explicites

## Résultats attendus

```
Passed!  - Failed:     0, Passed:    45, Skipped:     0, Total:    45
```