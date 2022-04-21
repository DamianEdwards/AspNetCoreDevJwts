using System.Diagnostics.CodeAnalysis;
using DevJwts.Lib;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication;

public class WebApplicationAuthenticationBuilder : AuthenticationBuilder
{
    private readonly WebApplicationBuilder _appBuilder;
    private AuthenticationBuilder? _authBuilder;

    public WebApplicationAuthenticationBuilder(WebApplicationBuilder appBuilder, string? defaultScheme)
        : base(appBuilder.Services)
    {
        _appBuilder = appBuilder;
        DefaultScheme = defaultScheme;
    }

    internal string? DefaultScheme { get; }

    public override AuthenticationBuilder AddPolicyScheme(string authenticationScheme, string? displayName, Action<PolicySchemeOptions> configureOptions)
    {
        AddAuthentication();

        return _authBuilder.AddPolicyScheme(authenticationScheme, displayName, configureOptions);
    }

    public override AuthenticationBuilder AddRemoteScheme<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(string authenticationScheme, string? displayName, Action<TOptions>? configureOptions)
    {
        AddAuthentication();

        return _authBuilder.AddRemoteScheme<TOptions, THandler>(authenticationScheme, displayName, configureOptions);
    }

    public override AuthenticationBuilder AddScheme<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(string authenticationScheme, Action<TOptions>? configureOptions)
    {
        return AddScheme<TOptions, THandler>(authenticationScheme, null, configureOptions);
    }

    public override AuthenticationBuilder AddScheme<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(string authenticationScheme, string? displayName, Action<TOptions>? configureOptions)
    {
        AddAuthentication();

        if (typeof(TOptions).IsAssignableFrom(typeof(JwtBearerOptions)) && typeof(THandler).IsAssignableFrom(typeof(JwtBearerHandler))
            && _appBuilder.Environment.IsDevelopment()
            && string.Equals(authenticationScheme, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            // Add dev JWTs support
            Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, DevJwtsConfigureOptions>();
            Services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>, DevJwtsConfigureOptions>();
            if (DefaultScheme is null)
            {
                Services.PostConfigureAll<AuthenticationOptions>(static c =>
                {
                    if (c.Schemes.SingleOrDefault() is { Name: JwtBearerDefaults.AuthenticationScheme } scheme && scheme.HandlerType == typeof(JwtBearerHandler))
                    {
                        // Set the default scheme to JwtBearer if it's the only scheme registered
                        c.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    }
                });
            }
        }

        return _authBuilder.AddScheme<TOptions, THandler>(authenticationScheme, displayName, configureOptions);
    }

    [MemberNotNull(nameof(_authBuilder))]
    private void AddAuthentication()
    {
        _authBuilder ??= DefaultScheme is null ? _appBuilder.Services.AddAuthentication() : _appBuilder.Services.AddAuthentication(DefaultScheme);
        _appBuilder.Services.AddAuthorization();
    }
}
