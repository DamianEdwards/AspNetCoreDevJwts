using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using DevJwts.Lib;

namespace Microsoft.AspNetCore.Builder;

public static class DevJwtsBuilderExtensions
{
    public static WebApplicationAuthenticationBuilder Authentication(this WebApplicationBuilder appBuilder, string? defaultScheme = null)
    {
        var authBuilder = appBuilder.Services.AddAuthentication();
        appBuilder.Services.AddAuthorization();

        var webAppAuthBuilder = new WebApplicationAuthenticationBuilder(appBuilder, authBuilder, defaultScheme);

        return webAppAuthBuilder;
    }

    public static WebApplicationAuthenticationBuilder AddDevJwtBearer(this WebApplicationAuthenticationBuilder builder)
    {
        if (builder.WebHostEnvironment.IsDevelopment())
        {
            builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, DevJwtsConfigureOptions>();
            builder.Services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>, DevJwtsConfigureOptions>();
            builder.Services.Configure<AuthenticationOptions>(c => c.DefaultScheme = builder.DefaultScheme ?? JwtBearerDefaults.AuthenticationScheme);
            builder.AddJwtBearer();
        }
        return builder;
    }
}
