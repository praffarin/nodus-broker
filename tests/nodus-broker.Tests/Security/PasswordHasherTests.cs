using FluentAssertions;
using MqttBroker.Security;
using Xunit;

namespace MqttBroker.Tests.Security;

/// <summary>
/// Tests unitaires pour la classe <see cref="PasswordHasher"/>.
/// </summary>
public class PasswordHasherTests
{
    [Fact]
    public void Hash_WithValidPassword_ReturnsHashInCorrectFormat()
    {
        // Arrange
        string password = "MySecurePassword123!";

        // Act
        string hash = PasswordHasher.Hash(password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().Contain(".");
        
        string[] parts = hash.Split('.');
        parts.Should().HaveCount(2);
        parts[0].Should().NotBeNullOrEmpty(); // Salt
        parts[1].Should().NotBeNullOrEmpty(); // Hash
    }

    [Fact]
    public void Hash_WithSamePassword_GeneratesDifferentHashes()
    {
        // Arrange
        string password = "MySecurePassword123!";

        // Act
        string hash1 = PasswordHasher.Hash(password);
        string hash2 = PasswordHasher.Hash(password);

        // Assert
        hash1.Should().NotBe(hash2, "car chaque hash utilise un sel aléatoire différent");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Hash_WithEmptyOrNullPassword_ThrowsArgumentException(string? password)
    {
        // Act
        Action act = () => PasswordHasher.Hash(password!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*mot de passe*vide*");
    }

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        string password = "MySecurePassword123!";
        string hash = PasswordHasher.Hash(password);

        // Act
        bool result = PasswordHasher.Verify(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        string password = "MySecurePassword123!";
        string wrongPassword = "WrongPassword456!";
        string hash = PasswordHasher.Hash(password);

        // Act
        bool result = PasswordHasher.Verify(wrongPassword, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "validhash.validhash")]
    [InlineData(null, "validhash.validhash")]
    [InlineData("password", "")]
    [InlineData("password", null)]
    public void Verify_WithEmptyOrNullInputs_ReturnsFalse(string? password, string? hash)
    {
        // Act
        bool result = PasswordHasher.Verify(password!, hash!);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("invalidformat")]
    [InlineData("only.one")]
    [InlineData("too.many.parts.here")]
    public void Verify_WithInvalidHashFormat_ReturnsFalse(string invalidHash)
    {
        // Arrange
        string password = "MySecurePassword123!";

        // Act
        bool result = PasswordHasher.Verify(password, invalidHash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithInvalidBase64InHash_ReturnsFalse()
    {
        // Arrange
        string password = "MySecurePassword123!";
        string invalidHash = "not-base64!@#.also-not-base64!@#";

        // Act
        bool result = PasswordHasher.Verify(password, invalidHash);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("a")]
    [InlineData("simple")]
    [InlineData("MyVeryLongPasswordWithSpecialChars!@#$%^&*()_+-=[]{}|;:',.<>?/")]
    public void Hash_AndVerify_WithVariousPasswordLengths_WorksCorrectly(string password)
    {
        // Act
        string hash = PasswordHasher.Hash(password);
        bool verified = PasswordHasher.Verify(password, hash);

        // Assert
        verified.Should().BeTrue();
    }

    [Fact]
    public void Verify_IsCaseSensitive()
    {
        // Arrange
        string password = "MyPassword";
        string hash = PasswordHasher.Hash(password);

        // Act
        bool verifyLower = PasswordHasher.Verify("mypassword", hash);
        bool verifyUpper = PasswordHasher.Verify("MYPASSWORD", hash);
        bool verifyCorrect = PasswordHasher.Verify("MyPassword", hash);

        // Assert
        verifyLower.Should().BeFalse();
        verifyUpper.Should().BeFalse();
        verifyCorrect.Should().BeTrue();
    }
}