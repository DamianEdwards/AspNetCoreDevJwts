using System.Diagnostics.CodeAnalysis;
using DevJwts.Lib;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
            builder.Services.AddSingleton<IStartupFilter, DevJwtsStartupFilter>();
            builder.Services.Configure<AuthenticationOptions>(c => c.DefaultScheme = builder.DefaultScheme ?? JwtBearerDefaults.AuthenticationScheme);
            builder.AddJwtBearer();
        }
        return builder;
    }
}

public class WebApplicationAuthenticationBuilder : AuthenticationBuilder
{
    private readonly WebApplicationBuilder _appBuilder;
    private readonly AuthenticationBuilder _authBuilder;

    public WebApplicationAuthenticationBuilder(WebApplicationBuilder appBuilder, AuthenticationBuilder authBuilder, string? defaultScheme)
        : base(authBuilder.Services)
    {
        _appBuilder = appBuilder;
        _authBuilder = authBuilder;
        WebHostEnvironment = _appBuilder.Environment;
        DefaultScheme = defaultScheme;
    }

    internal IWebHostEnvironment WebHostEnvironment { get; }

    internal string? DefaultScheme { get; }

    public override IServiceCollection Services => _authBuilder.Services;

    public override AuthenticationBuilder AddPolicyScheme(string authenticationScheme, string? displayName, Action<PolicySchemeOptions> configureOptions)
    {
        return _authBuilder.AddPolicyScheme(authenticationScheme, displayName, configureOptions);
    }

    public override AuthenticationBuilder AddRemoteScheme<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(string authenticationScheme, string? displayName, Action<TOptions>? configureOptions)
    {
        return _authBuilder.AddRemoteScheme<TOptions, THandler>(authenticationScheme, displayName, configureOptions);
    }

    public override AuthenticationBuilder AddScheme<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(string authenticationScheme, Action<TOptions>? configureOptions)
    {
        return _authBuilder.AddScheme<TOptions, THandler>(authenticationScheme, configureOptions);
    }

    public override AuthenticationBuilder AddScheme<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(string authenticationScheme, string? displayName, Action<TOptions>? configureOptions)
    {
        return _authBuilder.AddScheme<TOptions, THandler>(authenticationScheme, displayName, configureOptions);
    }
}
