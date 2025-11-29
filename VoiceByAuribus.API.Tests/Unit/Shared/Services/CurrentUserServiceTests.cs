using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using VoiceByAuribus_API.Shared.Infrastructure.Services;

namespace VoiceByAuribus_API.Tests.Unit.Shared.Services;

/// <summary>
/// Unit tests for CurrentUserService.
/// Tests JWT claims extraction and user context management.
/// </summary>
public class CurrentUserServiceTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor;

    public CurrentUserServiceTests()
    {
        _httpContextAccessor = new Mock<IHttpContextAccessor>();
    }

    [Fact]
    public void UserId_WithValidSubClaim_ReturnsGuid()
    {
        // Arrange
        var expectedUserId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("sub", expectedUserId.ToString())
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var userId = service.UserId;

        // Assert
        userId.Should().Be(expectedUserId);
    }

    [Fact]
    public void UserId_WithoutSubClaim_ReturnsNull()
    {
        // Arrange
        SetupHttpContext(Array.Empty<Claim>());
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var userId = service.UserId;

        // Assert
        userId.Should().BeNull();
    }

    [Fact]
    public void UserId_WithInvalidSubClaim_ReturnsNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", "not-a-valid-guid")
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var userId = service.UserId;

        // Assert
        userId.Should().BeNull();
    }

    [Fact]
    public void Username_WithPreferredUsernameClaim_ReturnsUsername()
    {
        // Arrange
        var expectedUsername = "john.doe";
        var claims = new[]
        {
            new Claim("preferred_username", expectedUsername)
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var username = service.Username;

        // Assert
        username.Should().Be(expectedUsername);
    }

    [Fact]
    public void Username_WithUsernameClaim_ReturnsUsername()
    {
        // Arrange
        var expectedUsername = "jane.doe";
        var claims = new[]
        {
            new Claim("username", expectedUsername)
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var username = service.Username;

        // Assert
        username.Should().Be(expectedUsername);
    }

    [Fact]
    public void Username_WithClientIdClaim_ReturnsClientId()
    {
        // Arrange
        var expectedClientId = "m2m-client-123";
        var claims = new[]
        {
            new Claim("client_id", expectedClientId)
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var username = service.Username;

        // Assert
        username.Should().Be(expectedClientId);
    }

    [Fact]
    public void Username_PreferredUsernameTakesPrecedence()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("preferred_username", "preferred"),
            new Claim("username", "fallback1"),
            new Claim("client_id", "fallback2")
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var username = service.Username;

        // Assert
        username.Should().Be("preferred");
    }

    [Fact]
    public void Email_WithEmailClaim_ReturnsEmail()
    {
        // Arrange
        var expectedEmail = "user@example.com";
        var claims = new[]
        {
            new Claim(ClaimTypes.Email, expectedEmail)
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var email = service.Email;

        // Assert
        email.Should().Be(expectedEmail);
    }

    [Fact]
    public void Email_WithLowercaseEmailClaim_ReturnsEmail()
    {
        // Arrange
        var expectedEmail = "user@example.com";
        var claims = new[]
        {
            new Claim("email", expectedEmail)
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var email = service.Email;

        // Assert
        email.Should().Be(expectedEmail);
    }

    [Fact]
    public void IsAuthenticated_WithAuthenticatedUser_ReturnsTrue()
    {
        // Arrange
        var claims = new[] { new Claim("sub", Guid.NewGuid().ToString()) };
        SetupHttpContext(claims, isAuthenticated: true);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var isAuthenticated = service.IsAuthenticated;

        // Assert
        isAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_WithoutUser_ReturnsFalse()
    {
        // Arrange
        SetupHttpContext(null);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var isAuthenticated = service.IsAuthenticated;

        // Assert
        isAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void Scopes_WithSingleScopeClaim_ReturnsParsedScopes()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("scope", "voice-by-auribus-api/base voice-by-auribus-api/admin")
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var scopes = service.Scopes;

        // Assert
        scopes.Should().HaveCount(2);
        scopes.Should().Contain("voice-by-auribus-api/base");
        scopes.Should().Contain("voice-by-auribus-api/admin");
    }

    [Fact]
    public void Scopes_WithCognitoGroups_IncludesGroups()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("scope", "voice-by-auribus-api/base"),
            new Claim("cognito:groups", "Admins"),
            new Claim("cognito:groups", "PowerUsers")
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var scopes = service.Scopes;

        // Assert
        scopes.Should().HaveCount(3);
        scopes.Should().Contain("voice-by-auribus-api/base");
        scopes.Should().Contain("Admins");
        scopes.Should().Contain("PowerUsers");
    }

    [Fact]
    public void Scopes_WithoutScopeClaims_ReturnsEmptyCollection()
    {
        // Arrange
        SetupHttpContext(Array.Empty<Claim>());
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var scopes = service.Scopes;

        // Assert
        scopes.Should().BeEmpty();
    }

    [Fact]
    public void Scopes_IsCaseInsensitive()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("scope", "Voice-By-Auribus-API/Base")
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var hasScope = service.HasScope("voice-by-auribus-api/base");

        // Assert
        hasScope.Should().BeTrue();
    }

    [Fact]
    public void HasScope_WithExistingScope_ReturnsTrue()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("scope", "voice-by-auribus-api/base voice-by-auribus-api/admin")
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var hasBase = service.HasScope("voice-by-auribus-api/base");
        var hasAdmin = service.HasScope("voice-by-auribus-api/admin");

        // Assert
        hasBase.Should().BeTrue();
        hasAdmin.Should().BeTrue();
    }

    [Fact]
    public void HasScope_WithNonExistingScope_ReturnsFalse()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("scope", "voice-by-auribus-api/base")
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var hasAdmin = service.HasScope("voice-by-auribus-api/admin");

        // Assert
        hasAdmin.Should().BeFalse();
    }

    [Fact]
    public void IsAdmin_WithAdminScope_ReturnsTrue()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("scope", "voice-by-auribus-api/admin")
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var isAdmin = service.IsAdmin;

        // Assert
        isAdmin.Should().BeTrue();
    }

    [Fact]
    public void IsAdmin_WithoutAdminScope_ReturnsFalse()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("scope", "voice-by-auribus-api/base")
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var isAdmin = service.IsAdmin;

        // Assert
        isAdmin.Should().BeFalse();
    }

    [Fact]
    public void Scopes_IsCached_DoesNotRecomputeOnMultipleCalls()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("scope", "voice-by-auribus-api/base")
        };
        SetupHttpContext(claims);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act
        var scopes1 = service.Scopes;
        var scopes2 = service.Scopes;

        // Assert
        scopes1.Should().BeSameAs(scopes2, "scopes should be cached");
    }

    [Fact]
    public void AllProperties_WithNullHttpContext_HandleGracefully()
    {
        // Arrange
        _httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act & Assert - Should not throw
        service.UserId.Should().BeNull();
        service.Username.Should().BeNull();
        service.Email.Should().BeNull();
        service.IsAuthenticated.Should().BeFalse();
        service.Scopes.Should().BeEmpty();
        service.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public void CompleteUserContext_WithAllClaims_ReturnsAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("preferred_username", "john.doe"),
            new Claim(ClaimTypes.Email, "john@example.com"),
            new Claim("scope", "voice-by-auribus-api/base voice-by-auribus-api/admin"),
            new Claim("cognito:groups", "PowerUsers")
        };
        SetupHttpContext(claims, isAuthenticated: true);
        var service = new CurrentUserService(_httpContextAccessor.Object);

        // Act & Assert
        service.UserId.Should().Be(userId);
        service.Username.Should().Be("john.doe");
        service.Email.Should().Be("john@example.com");
        service.IsAuthenticated.Should().BeTrue();
        service.Scopes.Should().HaveCount(3);
        service.HasScope("voice-by-auribus-api/base").Should().BeTrue();
        service.HasScope("voice-by-auribus-api/admin").Should().BeTrue();
        service.HasScope("PowerUsers").Should().BeTrue();
        service.IsAdmin.Should().BeTrue();
    }

    // Helper methods

    private void SetupHttpContext(IEnumerable<Claim>? claims, bool isAuthenticated = true)
    {
        if (claims == null)
        {
            _httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            return;
        }

        var identity = new ClaimsIdentity(claims, isAuthenticated ? "TestAuth" : null);
        var claimsPrincipal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = claimsPrincipal };
        
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
    }
}
