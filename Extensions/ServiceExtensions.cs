using System.Data;
using Microsoft.Data.SqlClient;
using zListBack.Repositories;

namespace zListBack.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register Dapper connection
            services.AddScoped<IDbConnection>(sp =>
                new SqlConnection(configuration.GetConnectionString("DefaultConnection")));

            // Register Repositories
            services.AddScoped<UserRepository>();
            services.AddScoped<ListRepository>();
            services.AddScoped<RefreshTokenRepository>();

            return services;
        }
    }
}
