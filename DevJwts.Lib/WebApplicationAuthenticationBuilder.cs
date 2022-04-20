using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Authentication;

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
