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

    public DevJwtsConfigureOptions(IConfiguration configuration, IServer server, ILogger<DevJwtsConfigureOptions> logger)
    {
        _configuration = configuration;
        _server = server;
        _logger = logger;
    }

    public void Configure(JwtBearerOptions options)
    {

    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            var addresses = _server.Features.Get<IServerAddressesFeature>();
            var httpsAddresses = addresses?.Addresses.Where(a => a.StartsWith("https")).ToList() ??
                throw new InvalidOperationException("Audience: What to do when there's no HTTPS address?");
            // TODO: React to key changing while app is running?
            var signingKeys = new[] { GetSigningKey(_configuration, _logger) };
            options.ClaimsIssuer = DevJwtsDefaults.Issuer;
            options.TokenValidationParameters.ValidAudiences = options.TokenValidationParameters.ValidAudiences?.Concat(httpsAddresses) ?? httpsAddresses;
            options.TokenValidationParameters.ValidIssuers = options.TokenValidationParameters.ValidIssuers?.Concat(_devJwtIssuers) ?? _devJwtIssuers;
            options.TokenValidationParameters.IssuerSigningKeys = options.TokenValidationParameters.IssuerSigningKeys?.Concat(signingKeys) ?? signingKeys;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;
        }
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
