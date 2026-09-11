using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SD.LocalCoder.AI.Shared.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSharedLayer(this IServiceCollection services, IConfiguration configuration)
        {
            return services;
        }
    }
}
