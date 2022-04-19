using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DevJwts.Lib;
public class DevJwtsStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            builder.UseRouting();

            // BUG: These middleware don't apply to the main pipeline :(
            builder.UseAuthentication();
            builder.UseAuthorization();

            builder.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/devjwt/create", (CreateJwt input, IOptionsSnapshot<JwtBearerOptions> jwtOptionsSnapshot) =>
                {
                    var jwtOptions = jwtOptionsSnapshot.Get(JwtBearerDefaults.AuthenticationScheme);
                    var jwtExpireInDays = 28;
                    var handler = jwtOptions.SecurityTokenValidators.OfType<JwtSecurityTokenHandler>().First();
                    var identity = new JwtIdentity(input.Name);
                    if (input.Claims is { Count: > 0 } claimsToAdd)
                    {
                        identity.AddClaims(claimsToAdd.Select(kvp => new Claim(kvp.Key, kvp.Value)));
                    }
                    var jwtSigningCredentials = new SigningCredentials(jwtOptions.TokenValidationParameters.IssuerSigningKey, SecurityAlgorithms.HmacSha256Signature);
                    var token = handler.CreateEncodedJwt(jwtOptions.TokenValidationParameters.ValidIssuer, jwtOptions.Audience, identity, DateTime.UtcNow, DateTime.UtcNow.AddDays(jwtExpireInDays), DateTime.UtcNow, jwtSigningCredentials);

                    return token;
                })
                .WithTags("AspNetCoreDevJwts");

                endpoints.MapPost("/devjwt/validate", (ValidateJwt input, IOptionsSnapshot<JwtBearerOptions> jwtOptionsSnapshot) =>
                {
                    var jwtOptions = jwtOptionsSnapshot.Get(JwtBearerDefaults.AuthenticationScheme);
                    var handler = jwtOptions.SecurityTokenValidators.OfType<JwtSecurityTokenHandler>().First();
                    var validationParameters = new TokenValidationParameters
                    {
                        IssuerSigningKey = jwtOptions.TokenValidationParameters.IssuerSigningKey,
                        ValidAudience = jwtOptions.Audience,
                        ValidIssuer = jwtOptions.TokenValidationParameters.ValidIssuer,
                    };
                    var principal = handler.ValidateToken(input.Token, validationParameters, out var validatedToken);
                    var jwt = (JwtSecurityToken)validatedToken;

                    return new RawJwt(jwt.Header, jwt.Payload);
                })
                .WithTags("AspNetCoreDevJwts")
                .Produces(statusCode: 200, contentType: "application/json");
            });

            next(builder);
        };
    }

    record CreateJwt(string Name)
    {
        public IDictionary<string, string>? Claims { get; init; }
    }

    record ValidateJwt(string Token);

    record RawJwt(JwtHeader Header, JwtPayload Payload);

    class JwtIdentity : ClaimsIdentity
    {
        public JwtIdentity(string username)
            : base(new[] { new Claim(ClaimTypes.Name, username) }, "JwtBearer")
        {

        }
    }
}
