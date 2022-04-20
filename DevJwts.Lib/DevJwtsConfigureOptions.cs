﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DevJwts.Lib;

public class DevJwtsConfigureOptions : IConfigureOptions<JwtBearerOptions>, IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IConfiguration _configuration;
    private readonly IServer _server;
    private readonly ILogger<DevJwtsConfigureOptions> _logger;

    public DevJwtsConfigureOptions(IConfiguration configuration, IServer server, ILogger<DevJwtsConfigureOptions> logger)
    {
        _configuration = configuration;
        _server = server;
        _logger = logger;

        SigningKey = GetSigningKey(_configuration, _logger);
    }

    public SecurityKey? SigningKey { get; }

    public static string Issuer => "AspNetCoreDevJwt";

    public void Configure(JwtBearerOptions options)
    {
        var addresses = _server.Features.Get<IServerAddressesFeature>();
        var firstHttps = addresses?.Addresses.First(a => a.StartsWith("https"));
        options.Audience = firstHttps ?? "https://localhost:5001";
        options.ClaimsIssuer = Issuer;
        options.TokenValidationParameters.ValidIssuer = Issuer;
        options.TokenValidationParameters.IssuerSigningKey = SigningKey;
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        Configure(options);
    }

    private static SecurityKey GetSigningKey(IConfiguration configuration, ILogger logger)
    {
        var jwtKeyMaterialSecret = configuration["AspNetCoreDevJwts:KeyMaterial"];
        if (jwtKeyMaterialSecret is null)
        {
            logger.LogWarning(
                """
                   Stable key for AspNetCoreDevJwts was not found in app configuration. Add a user secret containing a stable key to enable JWT verification across app restarts:
                       MyApp> dotnet user-secrets init
                       MyApp> dotnet user-secrets set "AspNetCoreDevJwts:KeyMaterial" C3F184B1106CD90FB0D8CD44AA2BE378
                """);
        }
        var jwtKeyMaterial = !string.IsNullOrEmpty(jwtKeyMaterialSecret)
            ? Convert.FromHexString(jwtKeyMaterialSecret)
            : System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        return new SymmetricSecurityKey(jwtKeyMaterial);
    }
}
