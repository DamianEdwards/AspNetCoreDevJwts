using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

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
}
