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
            {
                var conn = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
                conn.Open();
                return conn;
            });

            // Register Repositories
            services.AddScoped<UserRepository>();
            services.AddScoped<ListRepository>();
            services.AddScoped<RefreshTokenRepository>();

            return services;
        }
    }
}
