# RotatingJwt

## Overview

RotatingJwt is a secure authentication system that implements **JWT (JSON Web Tokens) with fingerprinting and rotation**. It enhances security by regularly rotating tokens and using **AES encryption** for additional protection.

## Features

- 🔐 **JWT Token Authentication** with custom expiration policies.
- 🔄 **Token Rotation** to prevent token reuse.
- 🛡️ **AES Encryption** for sensitive data.
- ⚡ **Built-in Middleware** for easy integration.
- 🔍 **Fingerprinting Mechanism** for added security.

## Installation

To use **JisaSoftech.JWT.Rotation**, install the required dependencies:

```sh
dotnet add package JisaSoftech.JWT.Rotation
```

## Configuration

### 1️⃣ **Register RotatingJwt in ASP.NET Core**

Modify `Program.cs` to configure authentication:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Security.Cryptography;
using RotatingJwt;

var builder = WebApplication.CreateBuilder(args);

// Configure Rotating JWT
builder.Services.AddRotatingJwt(options =>
{
    options.Config = new RotatingJwtOptions
   {
      TokenLifeTime = TimeSpan.FromMinutes(20),
      RefreshTokenLifeTime = TimeSpan.FromMinutes(40),
      AesKeySize = 256,
      Audience = "https://localhost:7054/",
      Issuer = "https://localhost:7054/",
   };

   // Create a random AES
    using var aes = Aes.Create();
    aes.KeySize = 256;
    aes.GenerateKey();

    // Or can use any custom AES key can be from key vault
    options.Config.SecretKey = Convert.ToBase64String(aes.Key);
    options.Config.AesKeySize = aes.KeySize;
    return options;
});

// Add authentication and authorization middleware
builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

### 2️⃣ **Create Authentication Controller**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace RotatingJwtExample.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(JwtTokenService jwtTokenService) : ControllerBase
    {
        [HttpPost("token")]
        public IActionResult GenerateToken()
        {
            var userId = Guid.NewGuid().ToString();
            var tokenResponse = jwtTokenService.GenerateAccessToken(userId);
            return Ok(tokenResponse);
        }

        [HttpPost("validate")]
        public IActionResult ValidateToken([FromBody] string token)
        {
            var validationResult = jwtTokenService.ValidateParameters(token);
            if (!validationResult.IsValid)
                return Unauthorized(validationResult.Error);

            return Ok(new
            {
                Message = "Token is valid",
                Claims = validationResult.ClaimsPrincipal?.Claims.Select(c => new { c.Type, c.Value })
            });
        }

        [Authorize]
        [HttpGet("protected")]
        public IActionResult ProtectedResource()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Ok(new { Message = "You have accessed a protected resource!", UserId = userId });
        }
    }
}
```

## Usage

### 🔹 **Generate JWT Token**

```http
POST https://localhost:7054/api/auth/token
```

### 🔹 **Validate JWT Token**

```http
POST https://localhost:7054/api/auth/validate
Content-Type: application/json
{
    "token": "<Your_JWT_Token_Here>"
}
```

### 🔹 **Access Protected Resource**

```http
GET https://localhost:7054/api/auth/protected
Authorization: Bearer <Your_JWT_Token_Here>
```

## XML Documentation

### **AES Encryption Helper**

- `Encrypt(string plainText, string keyString)`: Encrypts text using AES.
- `Decrypt(string cipherTextWithIv, string keyString)`: Decrypts AES text.
- `ConvertKeyStringToBytes(string keyString)`: Converts Base64 key to bytes.

### **Custom Authentication Handler**

- `HandleAuthenticateAsync()`: Handles authentication.
- `HandleChallengeAsync(AuthenticationProperties properties)`: Handles unauthorized access.
- `HandleForbiddenAsync(AuthenticationProperties properties)`: Handles forbidden requests.
- `GenerateFingerprint(HttpContext context)`: Generates request fingerprint.

### **Jwt Configuration**

- `Config`: Stores JWT options.
- `TokenValidationParameters`: Validation settings.

### **Jwt Token Service**

- `GenerateAccessToken(string userId)`: Generates a token.
- `ValidateParameters()`: Validates extracted tokens.
- `ValidateParameters(string token)`: Validates a given token.

### **Token Response**

- `Token`: JWT access token.
- `PrivateKey`: Encrypted private key.
- `PublicKey`: Encrypted public key.

## License

This project is licensed under the **MIT License**.
