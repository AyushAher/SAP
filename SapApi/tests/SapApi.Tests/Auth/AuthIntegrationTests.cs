using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SapApi.Shared.Models;
using SapApi.Shared.Responses.Account;

namespace SapApi.Tests.Auth;

[TestFixture]
public class AuthIntegrationTests
{
    private const string TestCompanyDb = "PBBPL_UAT";

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Register_ThenLogin_ReturnsJwtToken()
    {
        var userName = $"user_{Guid.NewGuid():N}";
        var email = $"{userName}@test.com";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Integration User",
            userName,
            email,
            password = "Test123!",
            companyDb = TestCompanyDb,
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<RegisterUserResponse>>();
        registerBody!.Success.Should().BeTrue();
        registerBody.Data!.Succeeded.Should().BeTrue();

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            userName,
            password = "Test123!",
            companyDb = TestCompanyDb,
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        loginBody!.Success.Should().BeTrue();
        loginBody.Data!.Token.Should().NotBeNullOrWhiteSpace();
        loginBody.Data.Claims.Should().Contain(c => c.Type.Contains("role", StringComparison.OrdinalIgnoreCase));
        loginBody.Data.Claims.Should().Contain(c => c.Type == "CompanyDb" && c.Value == TestCompanyDb);
    }

    [Test]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var userName = $"user_{Guid.NewGuid():N}";
        var email = $"{userName}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Login Test User",
            userName,
            email,
            password = "Test123!",
            companyDb = TestCompanyDb,
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            userName,
            password = "WrongPassword1!",
            companyDb = TestCompanyDb,
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        body!.Success.Should().BeFalse();
    }

    [Test]
    public async Task Register_DuplicateUser_ReturnsBadRequest()
    {
        var userName = $"dup_{Guid.NewGuid():N}";
        var email = $"{userName}@test.com";
        var payload = new
        {
            fullName = "Duplicate User",
            userName,
            email,
            password = "Test123!",
            companyDb = TestCompanyDb,
        };

        (await _client.PostAsJsonAsync("/api/auth/register", payload)).StatusCode.Should().Be(HttpStatusCode.OK);
        var duplicate = await _client.PostAsJsonAsync("/api/auth/register", payload);
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task PublicKey_ReturnsPemKey()
    {
        var response = await _client.GetAsync("/api/auth/public-key");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("publicKey");
    }

    [Test]
    public async Task CompanyDatabases_ReturnsEnumValues()
    {
        var response = await _client.GetAsync("/api/auth/company-databases");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("PBBPL_LIVE");
        json.Should().Contain("PBBPL_UAT");
    }
}
