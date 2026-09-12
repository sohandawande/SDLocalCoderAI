using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SD.LocalCoder.AI.Git.Interfaces;
using SD.LocalCoder.AI.Git.Services;

namespace SD.LocalCoder.AI.Git.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddGitLayer(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IGitService, GitService>();
            return services;
        }
    }
}
