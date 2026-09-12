using Microsoft.Extensions.DependencyInjection;
using SD.LocalCoder.AI.Indexing.Services;

namespace SD.LocalCoder.AI.Indexing.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIndexingLayer(
        this IServiceCollection services)
    {
        services.AddSingleton<RepositoryIndexer>();
        return services;
    }
}
