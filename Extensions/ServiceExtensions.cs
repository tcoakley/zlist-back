using System.Data;
using Microsoft.Data.SqlClient;
using Stripe;
using zListBack.Configurations;
using zListBack.Repositories;
using zListBack.Services;
using AppSubscriptionService = zListBack.Services.SubscriptionService;

namespace zListBack.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Stripe
            var stripeSettings = configuration.GetSection("Stripe").Get<StripeSettings>()!;
            services.Configure<StripeSettings>(configuration.GetSection("Stripe"));
            StripeConfiguration.ApiKey = stripeSettings.SecretKey;

            // Register Dapper connection
            services.AddScoped<IDbConnection>(sp =>
            {
                var conn = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
                conn.Open();
                return conn;
            });

            // Register Repositories
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserPaymentHistoryRepository, UserPaymentHistoryRepository>();
            services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
            services.AddScoped<ListRepository>();
            services.AddScoped<AppVersionRepository>();
            services.AddScoped<RefreshTokenRepository>();

            // Register Services
            services.AddScoped<AppSubscriptionService>();
            services.AddHostedService<CleanupService>();

            return services;
        }
    }
}
