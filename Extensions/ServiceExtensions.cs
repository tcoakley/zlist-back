using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using zListBack.Data;
using zListBack.Repositories;

namespace zListBack.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register DbContext
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            // Register Repositories
            services.AddScoped<UserRepository>();

            // Add other services here as the app grows
            return services;
        }
    }
}
