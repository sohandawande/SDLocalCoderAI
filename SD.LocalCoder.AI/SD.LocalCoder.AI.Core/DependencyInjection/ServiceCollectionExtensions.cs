using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SD.LocalCoder.AI.Core.Interfaces;
using SD.LocalCoder.AI.Core.Services;
using SD.LocalCoder.AI.Indexing.DependencyInjection;

namespace SD.LocalCoder.AI.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IProjectIntelligenceService, ProjectIntelligenceService>();
        services.AddScoped<IRepositoryContextService, RepositoryContextService>();
        services.AddSingleton<ISessionService, SessionService>();

        services.AddIndexingLayer();

        return services;
    }
}
