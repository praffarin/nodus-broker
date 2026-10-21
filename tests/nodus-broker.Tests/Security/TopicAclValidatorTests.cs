using FluentAssertions;
using MqttBroker.Configuration;
using MqttBroker.Security;
using Xunit;

namespace MqttBroker.Tests.Security;

/// <summary>
/// Tests unitaires pour la classe <see cref="TopicAclValidator"/>.
/// </summary>
public class TopicAclValidatorTests
{
    [Fact]
    public void Constructor_WithValidRules_CreatesInstance()
    {
        // Arrange
        var rules = new Dictionary<string, List<AclRule>>();

        // Act
        var validator = new TopicAclValidator(rules);

        // Assert
        validator.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullRules_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new TopicAclValidator(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("rulesByRole");
    }

    [Fact]
    public void CanPublish_WithNoRoles_ReturnsFalse()
    {
        // Arrange
        var rules = CreateBasicRules();
        var validator = new TopicAclValidator(rules);

        // Act
        bool result = validator.CanPublish(new List<string>(), "client1", "test/topic");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanPublish_WithNullRoles_ReturnsFalse()
    {
        // Arrange
        var rules = CreateBasicRules();
        var validator = new TopicAclValidator(rules);

        // Act
        bool result = validator.CanPublish(null!, "client1", "test/topic");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CanPublish_WithEmptyOrNullTopic_ReturnsFalse(string? topic)
    {
        // Arrange
        var rules = CreateBasicRules();
        var validator = new TopicAclValidator(rules);

        // Act
        bool result = validator.CanPublish(new[] { "admin" }, "client1", topic!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanPublish_WithExactMatch_ReturnsTrue()
    {
        // Arrange
        var rules = new Dictionary<string, List<AclRule>>
        {
            ["device"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "sensor/temperature", Publish = true, Subscribe = false }
            }
        };
        var validator = new TopicAclValidator(rules);

        // Act
        bool result = validator.CanPublish(new[] { "device" }, "client1", "sensor/temperature");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanPublish_WithoutPublishPermission_ReturnsFalse()
    {
        // Arrange
        var rules = new Dictionary<string, List<AclRule>>
        {
            ["device"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "sensor/temperature", Publish = false, Subscribe = true }
            }
        };
        var validator = new TopicAclValidator(rules);

        // Act
        bool result = validator.CanPublish(new[] { "device" }, "client1", "sensor/temperature");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanPublish_WithSingleLevelWildcard_ReturnsTrue()
    {
        // Arrange
        var rules = new Dictionary<string, List<AclRule>>
        {
            ["device"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "sensor/+/temperature", Publish = true, Subscribe = false }
            }
        };
        var validator = new TopicAclValidator(rules);

        // Act
        bool result1 = validator.CanPublish(new[] { "device" }, "client1", "sensor/living/temperature");
        bool result2 = validator.CanPublish(new[] { "device" }, "client1", "sensor/bedroom/temperature");
        bool result3 = validator.CanPublish(new[] { "device" }, "client1", "sensor/temperature");

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeFalse("car + requiert exactement un niveau");
    }

    [Fact]
    public void CanPublish_WithMultiLevelWildcard_ReturnsTrue()
    {
        // Arrange
        var rules = new Dictionary<string, List<AclRule>>
        {
            ["admin"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "sensor/#", Publish = true, Subscribe = false }
            }
        };
        var validator = new TopicAclValidator(rules);

        // Act
        bool result1 = validator.CanPublish(new[] { "admin" }, "client1", "sensor/temperature");
        bool result2 = validator.CanPublish(new[] { "admin" }, "client1", "sensor/living/temperature");
        bool result3 = validator.CanPublish(new[] { "admin" }, "client1", "sensor/a/b/c/d");
        bool result4 = validator.CanPublish(new[] { "admin" }, "client1", "device/temperature");

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();
        result4.Should().BeFalse("car ne commence pas par sensor/");
    }

    [Fact]
    public void CanPublish_WithClientIdPlaceholder_ReplacesCorrectly()
    {
        // Arrange
        var rules = new Dictionary<string, List<AclRule>>
        {
            ["device"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "device/{clientid}/status", Publish = true, Subscribe = false }
            }
        };
        var validator = new TopicAclValidator(rules);

        // Act
        bool result1 = validator.CanPublish(new[] { "device" }, "device001", "device/device001/status");
        bool result2 = validator.CanPublish(new[] { "device" }, "device001", "device/device002/status");

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse("car le clientid ne correspond pas");
    }

    [Fact]
    public void CanPublish_WithMultipleRoles_ReturnsTrue()
    {
        // Arrange
        var rules = new Dictionary<string, List<AclRule>>
        {
            ["device"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "sensor/#", Publish = true, Subscribe = false }
            },
            ["admin"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "admin/#", Publish = true, Subscribe = false }
            }
        };
        var validator = new TopicAclValidator(rules);

        // Act
        bool result1 = validator.CanPublish(new[] { "device", "admin" }, "client1", "sensor/temperature");
        bool result2 = validator.CanPublish(new[] { "device", "admin" }, "client1", "admin/config");

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    [Fact]
    public void CanSubscribe_WithExactMatch_ReturnsTrue()
    {
        // Arrange
        var rules = new Dictionary<string, List<AclRule>>
        {
            ["device"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "sensor/temperature", Publish = false, Subscribe = true }
            }
        };
        var validator = new TopicAclValidator(rules);

        // Act
        bool result = validator.CanSubscribe(new[] { "device" }, "client1", "sensor/temperature");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanSubscribe_WithoutSubscribePermission_ReturnsFalse()
    {
        // Arrange
        var rules = new Dictionary<string, List<AclRule>>
        {
            ["device"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "sensor/temperature", Publish = true, Subscribe = false }
            }
        };
        var validator = new TopicAclValidator(rules);

        // Act
        bool result = validator.CanSubscribe(new[] { "device" }, "client1", "sensor/temperature");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanSubscribe_WithWildcards_ReturnsTrue()
    {
        // Arrange
        var rules = new Dictionary<string, List<AclRule>>
        {
            ["device"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "sensor/#", Publish = false, Subscribe = true }
            }
        };
        var validator = new TopicAclValidator(rules);

        // Act
        bool result = validator.CanSubscribe(new[] { "device" }, "client1", "sensor/temperature");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanPublish_WithUnknownRole_ReturnsFalse()
    {
        // Arrange
        var rules = CreateBasicRules();
        var validator = new TopicAclValidator(rules);

        // Act
        bool result = validator.CanPublish(new[] { "unknown_role" }, "client1", "sensor/temperature");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SystemTopics_DoNotMatchWildcards()
    {
        // Arrange
        var rules = new Dictionary<string, List<AclRule>>
        {
            ["device"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "#", Publish = false, Subscribe = true }
            }
        };
        var validator = new TopicAclValidator(rules);

        // Act
        bool result = validator.CanSubscribe(new[] { "device" }, "client1", "$SYS/broker/clients/connected");

        // Assert
        result.Should().BeFalse("car les topics système ne matchent pas les wildcards génériques");
    }

    private static Dictionary<string, List<AclRule>> CreateBasicRules()
    {
        return new Dictionary<string, List<AclRule>>
        {
            ["admin"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "#", Publish = true, Subscribe = true }
            },
            ["device"] = new List<AclRule>
            {
                new AclRule { TopicPattern = "sensor/#", Publish = true, Subscribe = false },
                new AclRule { TopicPattern = "device/{clientid}/#", Publish = true, Subscribe = true }
            }
        };
    }
}