using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SD.LocalCoder.AI.Core.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCoreLayer(this IServiceCollection services, IConfiguration configuration)
        {
            return services;
        }
    }
}
