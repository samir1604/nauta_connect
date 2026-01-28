using Microsoft.Extensions.DependencyInjection;
using NautaManager.Contracts;
using NautaManager.Parsers;
using NautaManager.Persistence;

namespace NautaManager.Configuration;

public static class NautaManagerConfig
{
    public static IServiceCollection AddNautaManagerConfig(
        this IServiceCollection services)
    {
        services
            .AddSingleton<INautaService, NautaService>()
            .AddSingleton<IDataParser, NautaDataParser>()
            .AddSingleton<ISessionManager, SessionManager>();

        return services;
    }
}
