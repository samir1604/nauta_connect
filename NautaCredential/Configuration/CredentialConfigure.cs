using Microsoft.Extensions.DependencyInjection;
using NautaCredential.Contracts;
using NautaCredential.DTO;

namespace NautaCredential.Configuration;

public static class CredentialConfigure
{
    public static IServiceCollection AddCredentialConfig(
       this IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ICredentialManager<UserCredentials>, NautaCredentialManager>();
        }

        return services;
    }
}
