using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DevJwts.Lib;

public class DevJwtsConfigureOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private static readonly string[] _devJwtIssuers = new[] { DevJwtsDefaults.Issuer };
    private readonly IConfiguration _configuration;
    private readonly IServer _server;
    private readonly ILogger<DevJwtsConfigureOptions> _logger;
    private readonly SecurityKey[] _signingKeys;

    public DevJwtsConfigureOptions(IConfiguration configuration, IServer server, ILogger<DevJwtsConfigureOptions> logger)
    {
        _configuration = configuration;
        _server = server;
        _logger = logger;

        // TODO: React to key changing while app is running?
        _signingKeys = new[] { GetSigningKey(_configuration, _logger) };
    }

    public void Configure(JwtBearerOptions options)
    {
        var addresses = _server.Features.Get<IServerAddressesFeature>();
        var firstHttps = addresses?.Addresses.First(a => a.StartsWith("https"));
        options.Audience = firstHttps ?? throw new InvalidOperationException("Audience: What to do when there's no HTTPS address?");
        options.ClaimsIssuer = DevJwtsDefaults.Issuer;
        options.TokenValidationParameters.ValidIssuers = options.TokenValidationParameters.ValidIssuers?.Concat(_devJwtIssuers) ?? _devJwtIssuers;
        options.TokenValidationParameters.IssuerSigningKeys = options.TokenValidationParameters.IssuerSigningKeys?.Concat(_signingKeys) ?? _signingKeys;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        Configure(options);
    }

    private static SecurityKey GetSigningKey(IConfiguration configuration, ILogger logger)
    {
        var jwtKeyMaterialSecret = configuration[DevJwtsDefaults.SigningKeyConfigurationKey];
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
            ? Convert.FromBase64String(jwtKeyMaterialSecret)
            : System.Security.Cryptography.RandomNumberGenerator.GetBytes(DevJwtsDefaults.SigningKeyLength);
        return new SymmetricSecurityKey(jwtKeyMaterial);
    }
}
