using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RotatingJwt;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Enums;
using SapApi.Shared.Requests.Account;

namespace SapApi.Tests.Auth;

[TestFixture]
public class AuthServiceTests
{
    private ServiceProvider _provider = null!;

    private static void AddTestIdentity(IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 1;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
    }

    [SetUp]
    public async Task SetUp()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SapApi-Super-Secret-Jwt-Key-Change-In-Production-32chars!",
                ["Jwt:Issuer"] = "SapApi",
                ["Jwt:Audience"] = "SapApi",
                ["ApplicationConfiguration:SapServiceLayerUrl"] = "https://localhost:50000",
                ["ApplicationConfiguration:SkipSapLoginOnUserAuth"] = "true",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddOptions<Shared.Configuration.ApplicationConfiguration>()
            .Configure(o =>
            {
                o.SapServiceLayerUrl = "https://localhost:50000";
                o.SkipSapLoginOnUserAuth = true;
            });

        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        AddTestIdentity(services);

        services.AddRotatingJwt(config);

        var rsa = new Mock<IRsaDecryptionService>();
        rsa.Setup(r => r.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
        services.AddSingleton(rsa.Object);
        services.AddScoped<ISapLoginService, NoOpSapLoginService>();
        services.AddScoped<AuthService>();

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await IdentityDataSeeder.SeedRolesAsync(roleManager);
    }

    [TearDown]
    public void TearDown() => _provider.Dispose();

    [Test]
    public async Task RegisterAsync_CreatesUserWithStandardRole()
    {
        using var scope = _provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AuthService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = $"unit_{Guid.NewGuid():N}@test.com";
        var userName = $"sapuser_{Guid.NewGuid():N}";
        var result = await sut.RegisterAsync(new RegisterUserRequest
        {
            FullName = "Test User",
            UserName = userName,
            Email = email,
            Password = "Test123!",
            CompanyDb = SapCompanyDatabase.PBBPL_UAT,
        });

        result.Success.Should().BeTrue();
        var user = await userManager.FindByNameAsync(userName);
        user.Should().NotBeNull();
        user!.FullName.Should().Be("Test User");
        user.Email.Should().Be(email);
        (await userManager.GetRolesAsync(user)).Should().Contain(Shared.Constants.Roles.Standard);
    }

    [Test]
    public async Task LoginAsync_ValidCredentials_ReturnsTokenAndClaims()
    {
        using var scope = _provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AuthService>();

        var email = $"login_{Guid.NewGuid():N}@test.com";
        var userName = $"loginuser_{Guid.NewGuid():N}";
        await sut.RegisterAsync(new RegisterUserRequest
        {
            FullName = "Login User",
            UserName = userName,
            Email = email,
            Password = "Test123!",
            CompanyDb = SapCompanyDatabase.PBBPL_UAT,
        });

        var login = await sut.LoginAsync(new LoginRequest { UserName = userName, Password = "Test123!", CompanyDb = SapCompanyDatabase.PBBPL_UAT });

        login.Success.Should().BeTrue();
        login.Data!.Token.Should().NotBeNullOrWhiteSpace();
        login.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
        login.Data.Claims.Should().Contain(c => c.Type.Contains("email", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task RefreshAsync_ValidRefreshToken_ReturnsNewToken()
    {
        using var scope = _provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AuthService>();

        var email = $"refresh_{Guid.NewGuid():N}@test.com";
        var userName = $"refreshuser_{Guid.NewGuid():N}";
        await sut.RegisterAsync(new RegisterUserRequest
        {
            FullName = "Refresh User",
            UserName = userName,
            Email = email,
            Password = "Test123!",
            CompanyDb = SapCompanyDatabase.PBBPL_UAT,
        });

        var login = await sut.LoginAsync(new LoginRequest { UserName = userName, Password = "Test123!", CompanyDb = SapCompanyDatabase.PBBPL_UAT });
        var refresh = await sut.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.Data!.RefreshToken!, CompanyDb = SapCompanyDatabase.PBBPL_UAT });

        refresh.Success.Should().BeTrue();
        refresh.Data!.Token.Should().NotBeNullOrWhiteSpace();
        refresh.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task LoginAsync_UnknownUser_ReturnsFailure()
    {
        using var scope = _provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AuthService>();

        var login = await sut.LoginAsync(new LoginRequest
        {
            UserName = "missing@test.com",
            Password = "Test123!",
            CompanyDb = SapCompanyDatabase.PBBPL_UAT,
        });

        login.Success.Should().BeFalse();
        login.ErrorCode.Should().Be(Shared.BaseErrorCodes.IncorrectCredentials);
    }

    [Test]
    public async Task LoginAsync_WrongPassword_ReturnsFailure()
    {
        using var scope = _provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AuthService>();

        var email = $"wrong_{Guid.NewGuid():N}@test.com";
        var userName = $"wronguser_{Guid.NewGuid():N}";
        await sut.RegisterAsync(new RegisterUserRequest
        {
            FullName = "Wrong Pass User",
            UserName = userName,
            Email = email,
            Password = "Test123!",
            CompanyDb = SapCompanyDatabase.PBBPL_UAT,
        });

        var login = await sut.LoginAsync(new LoginRequest { UserName = userName, Password = "WrongPass1!", CompanyDb = SapCompanyDatabase.PBBPL_UAT });

        login.Success.Should().BeFalse();
        login.ErrorCode.Should().Be(Shared.BaseErrorCodes.IncorrectCredentials);
    }

    [Test]
    public async Task RegisterAsync_SapValidationFails_DoesNotCreateUser()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SapApi-Super-Secret-Jwt-Key-Change-In-Production-32chars!",
                ["Jwt:Issuer"] = "SapApi",
                ["Jwt:Audience"] = "SapApi",
                ["ApplicationConfiguration:SapServiceLayerUrl"] = "https://localhost:50000",
                ["ApplicationConfiguration:SkipSapLoginOnUserAuth"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddOptions<Shared.Configuration.ApplicationConfiguration>()
            .Configure(o =>
            {
                o.SapServiceLayerUrl = "https://localhost:50000";
                o.SkipSapLoginOnUserAuth = false;
            });

        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        AddTestIdentity(services);
        services.AddRotatingJwt(config);

        var rsa = new Mock<IRsaDecryptionService>();
        rsa.Setup(r => r.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
        services.AddSingleton(rsa.Object);

        var sapLogin = new Mock<ISapLoginService>();
        sapLogin
            .Setup(s => s.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SapCompanyDatabase>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Shared.Exceptions.ApiErrorException(Shared.BaseErrorCodes.IncorrectCredentials, "SAP credentials are invalid. Please verify your username and password."));
        services.AddSingleton(sapLogin.Object);
        services.AddScoped<AuthService>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await IdentityDataSeeder.SeedRolesAsync(roleManager);

        var sut = scope.ServiceProvider.GetRequiredService<AuthService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userName = $"sapfail_{Guid.NewGuid():N}";
        var result = await sut.RegisterAsync(new RegisterUserRequest
        {
            FullName = "SAP Fail User",
            UserName = userName,
            Email = $"sapfail_{Guid.NewGuid():N}@test.com",
            Password = "Test123!",
            CompanyDb = SapCompanyDatabase.PBBPL_UAT,
        });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(Shared.BaseErrorCodes.IncorrectCredentials);
        (await userManager.FindByNameAsync(userName)).Should().BeNull();
        sapLogin.Verify(s => s.ValidateCredentialsAsync(userName, "Test123!", SapCompanyDatabase.PBBPL_UAT, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RegisterAsync_SapValidationSucceeds_CreatesUser()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SapApi-Super-Secret-Jwt-Key-Change-In-Production-32chars!",
                ["Jwt:Issuer"] = "SapApi",
                ["Jwt:Audience"] = "SapApi",
                ["ApplicationConfiguration:SapServiceLayerUrl"] = "https://localhost:50000",
                ["ApplicationConfiguration:SkipSapLoginOnUserAuth"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddOptions<Shared.Configuration.ApplicationConfiguration>()
            .Configure(o =>
            {
                o.SapServiceLayerUrl = "https://localhost:50000";
                o.SkipSapLoginOnUserAuth = false;
            });

        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        AddTestIdentity(services);
        services.AddRotatingJwt(config);

        var rsa = new Mock<IRsaDecryptionService>();
        rsa.Setup(r => r.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
        services.AddSingleton(rsa.Object);

        var sapLogin = new Mock<ISapLoginService>();
        sapLogin
            .Setup(s => s.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SapCompanyDatabase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton(sapLogin.Object);
        services.AddScoped<AuthService>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await IdentityDataSeeder.SeedRolesAsync(roleManager);

        var sut = scope.ServiceProvider.GetRequiredService<AuthService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userName = $"sapsuccess_{Guid.NewGuid():N}";
        var result = await sut.RegisterAsync(new RegisterUserRequest
        {
            FullName = "SAP Success User",
            UserName = userName,
            Email = $"sapsuccess_{Guid.NewGuid():N}@test.com",
            Password = "Test123!",
            CompanyDb = SapCompanyDatabase.PBBPL_UAT,
        });

        result.Success.Should().BeTrue();
        (await userManager.FindByNameAsync(userName)).Should().NotBeNull();
        sapLogin.Verify(s => s.ValidateCredentialsAsync(userName, "Test123!", SapCompanyDatabase.PBBPL_UAT, It.IsAny<CancellationToken>()), Times.Once);
        sapLogin.Verify(s => s.LoginWithUserCredentialsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SapCompanyDatabase>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
