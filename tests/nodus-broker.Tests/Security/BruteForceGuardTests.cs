using FluentAssertions;
using MqttBroker.Security;
using Xunit;

namespace MqttBroker.Tests.Security;

/// <summary>
/// Tests unitaires pour la classe <see cref="BruteForceGuard"/>.
/// </summary>
public class BruteForceGuardTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var guard = new BruteForceGuard(maxAttempts: 5, lockoutDurationSeconds: 60);

        // Assert
        guard.Should().NotBeNull();
        guard.TrackedIpCount.Should().Be(0);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(-1, 60)]
    public void Constructor_WithInvalidMaxAttempts_ThrowsArgumentOutOfRangeException(int maxAttempts, int lockoutDuration)
    {
        // Act
        Action act = () => new BruteForceGuard(maxAttempts, lockoutDuration);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*maxAttempts*");
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(5, -1)]
    public void Constructor_WithInvalidLockoutDuration_ThrowsArgumentOutOfRangeException(int maxAttempts, int lockoutDuration)
    {
        // Act
        Action act = () => new BruteForceGuard(maxAttempts, lockoutDuration);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*lockoutDurationSeconds*");
    }

    [Fact]
    public void IsLocked_WithNewIp_ReturnsFalse()
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);
        string ip = "192.168.1.100";

        // Act
        bool isLocked = guard.IsLocked(ip);

        // Assert
        isLocked.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsLocked_WithEmptyOrNullIp_ReturnsFalse(string? ip)
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);

        // Act
        bool isLocked = guard.IsLocked(ip!);

        // Assert
        isLocked.Should().BeFalse();
    }

    [Fact]
    public void RegisterFailure_BelowThreshold_DoesNotLockIp()
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);
        string ip = "192.168.1.100";

        // Act
        guard.RegisterFailure(ip);
        guard.RegisterFailure(ip);

        // Assert
        guard.IsLocked(ip).Should().BeFalse();
        guard.TrackedIpCount.Should().Be(1);
    }

    [Fact]
    public void RegisterFailure_AtThreshold_LocksIp()
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);
        string ip = "192.168.1.100";

        // Act
        guard.RegisterFailure(ip);
        guard.RegisterFailure(ip);
        guard.RegisterFailure(ip);

        // Assert
        guard.IsLocked(ip).Should().BeTrue();
    }

    [Fact]
    public void RegisterFailure_AboveThreshold_RemainsLocked()
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);
        string ip = "192.168.1.100";

        // Act
        for (int i = 0; i < 10; i++)
        {
            guard.RegisterFailure(ip);
        }

        // Assert
        guard.IsLocked(ip).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RegisterFailure_WithEmptyOrNullIp_DoesNotThrow(string? ip)
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);

        // Act
        Action act = () => guard.RegisterFailure(ip!);

        // Assert
        act.Should().NotThrow();
        guard.TrackedIpCount.Should().Be(0);
    }

    [Fact]
    public void RegisterSuccess_ResetsFailureCount()
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);
        string ip = "192.168.1.100";

        // Act
        guard.RegisterFailure(ip);
        guard.RegisterFailure(ip);
        guard.RegisterSuccess(ip);
        guard.RegisterFailure(ip);

        // Assert
        guard.IsLocked(ip).Should().BeFalse("car le succès a réinitialisé le compteur");
    }

    [Fact]
    public void RegisterSuccess_AfterLockout_UnlocksIp()
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);
        string ip = "192.168.1.100";

        // Act
        guard.RegisterFailure(ip);
        guard.RegisterFailure(ip);
        guard.RegisterFailure(ip);
        guard.IsLocked(ip).Should().BeTrue();
        
        guard.RegisterSuccess(ip);

        // Assert
        guard.IsLocked(ip).Should().BeFalse();
        guard.TrackedIpCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RegisterSuccess_WithEmptyOrNullIp_DoesNotThrow(string? ip)
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);

        // Act
        Action act = () => guard.RegisterSuccess(ip!);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void IsLocked_AfterLockoutExpires_ReturnsFalse()
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 1);
        string ip = "192.168.1.100";

        // Act
        guard.RegisterFailure(ip);
        guard.RegisterFailure(ip);
        guard.RegisterFailure(ip);
        guard.IsLocked(ip).Should().BeTrue();

        // Attendre l'expiration du verrouillage
        Thread.Sleep(1100);

        // Assert
        guard.IsLocked(ip).Should().BeFalse("car le verrouillage a expiré");
        guard.TrackedIpCount.Should().Be(0, "car l'entrée expirée est supprimée");
    }

    [Fact]
    public void TrackedIpCount_WithMultipleIps_ReturnsCorrectCount()
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);

        // Act
        guard.RegisterFailure("192.168.1.1");
        guard.RegisterFailure("192.168.1.2");
        guard.RegisterFailure("192.168.1.3");

        // Assert
        guard.TrackedIpCount.Should().Be(3);
    }

    [Fact]
    public void RegisterFailure_WithMultipleIps_TracksIndependently()
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);
        string ip1 = "192.168.1.1";
        string ip2 = "192.168.1.2";

        // Act
        guard.RegisterFailure(ip1);
        guard.RegisterFailure(ip1);
        guard.RegisterFailure(ip1);
        
        guard.RegisterFailure(ip2);

        // Assert
        guard.IsLocked(ip1).Should().BeTrue();
        guard.IsLocked(ip2).Should().BeFalse();
    }

    [Fact]
    public void RegisterSuccess_OnlyAffectsSpecifiedIp()
    {
        // Arrange
        var guard = new BruteForceGuard(maxAttempts: 3, lockoutDurationSeconds: 60);
        string ip1 = "192.168.1.1";
        string ip2 = "192.168.1.2";

        // Act
        guard.RegisterFailure(ip1);
        guard.RegisterFailure(ip1);
        guard.RegisterFailure(ip2);
        guard.RegisterFailure(ip2);
        
        guard.RegisterSuccess(ip1);

        // Assert
        guard.TrackedIpCount.Should().Be(1, "car seule ip2 est encore suivie");
        guard.IsLocked(ip1).Should().BeFalse();
    }
}