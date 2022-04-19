using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DevJwts.Lib;
public class DevJwtsConfigureOptions : IConfigureOptions<JwtBearerOptions>, IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IConfiguration _configuration;
    private readonly IServer _server;

    public DevJwtsConfigureOptions(IConfiguration configuration, IServer server)
    {
        _configuration = configuration;
        _server = server;

        SigningKey = GetSigningKey(_configuration);
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

    private static SecurityKey GetSigningKey(IConfiguration configuration)
    {
        var jwtKeyMaterialSecret = configuration["AspNetCoreDevJwt:KeyMaterial"];
        var jwtKeyMaterial = !string.IsNullOrEmpty(jwtKeyMaterialSecret)
            ? Convert.FromHexString(jwtKeyMaterialSecret)
            : System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        return new SymmetricSecurityKey(jwtKeyMaterial);
    }
}
