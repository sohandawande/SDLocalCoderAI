using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SD.LocalCoder.AI.Core.Interfaces;
using SD.LocalCoder.AI.Core.Services;

namespace SD.LocalCoder.AI.Core.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCoreLayer(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IChatService, ChatService>();
            services.AddSingleton<ISessionService, SessionService>();
            return services;
        }
    }
}
