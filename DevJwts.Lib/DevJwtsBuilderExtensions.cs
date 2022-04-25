using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Microsoft.AspNetCore.Builder;

public static class DevJwtsBuilderExtensions
{
    private readonly static object _instanceKey = new();

    public static WebApplicationAuthenticationBuilder Authentication(this WebApplicationBuilder appBuilder, string? defaultScheme = null)
    {
        if (!appBuilder.Host.Properties.TryGetValue(_instanceKey, out var webAppAuthBuilder))
        {
            webAppAuthBuilder = new WebApplicationAuthenticationBuilder(appBuilder, defaultScheme);
            appBuilder.Host.Properties.Add(_instanceKey, webAppAuthBuilder);
        }

        return (WebApplicationAuthenticationBuilder)webAppAuthBuilder;
    }

    /// <summary>
    /// Adds an authorization policy with the specified roles to the endpoint(s).
    /// </summary>
    /// <typeparam name="TBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="roles"></param>
    /// <returns></returns>
    public static TBuilder RequireRole<TBuilder>(this TBuilder builder, params string[] roles) where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization(p => p.RequireRole(roles));
        return builder;
    }

    /// <summary>
    /// Adds an authorization policy with the specified scope claims to the endpoint(s).
    /// </summary>
    /// <typeparam name="TBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="scopes"></param>
    /// <returns></returns>
    public static TBuilder RequireScope<TBuilder>(this TBuilder builder, params string[] scopes) where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization(p => p.RequireClaim("scope", scopes));
        return builder;
    }
}
