using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Web;
using OpenTelemetry.Logs;
using System.Text;
using zListBack.Extensions;
using zListBack.Hubs;
using zListBack.Services;
using zListBack.Utils;

NLog.LogManager.Setup().LoadConfigurationFromFile("nlog.config");

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseNLog();

// UseAzureMonitor() throws at startup if no connection string is configured anywhere, instead of
// no-op'ing — only wire it up when one is actually present (i.e. in Azure, via the App Service
// setting). Local dev has none on purpose, so this is skipped entirely there.
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor(o =>
    {
        // Warning/Error logs should never be dropped just because their parent request wasn't sampled
        o.EnableTraceBasedLogsSampler = false;
    });

    // Only Warning/Error (and above) logs are sent to Application Insights, to keep ingestion volume/cost down.
    // NLog's own file/console targets are unaffected and keep their existing levels.
    builder.Logging.AddFilter<OpenTelemetryLoggerProvider>("", Microsoft.Extensions.Logging.LogLevel.Warning);
}

// Required for Stripe webhook signature verification — must read raw body
builder.Services.Configure<RouteOptions>(options => { });

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new NullableDateTimeConverter());
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();

builder.Services.AddApplicationServices(builder.Configuration);

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ContactService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ListService>();
builder.Services.AddScoped<RecaptchaService>();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["JwtSettings:Key"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            throw new InvalidOperationException("JwtSettings:Key is missing in configuration.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/run"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:4200",
                    "https://zchecklist.com",
                    "https://www.zchecklist.com",
                    "https://blue-glacier-08208d91e.7.azurestaticapps.net")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// Answers Azure App Service's "Always On" keep-warm ping so it doesn't show up as a 404 in Application Insights
app.MapGet("/", () => Results.Ok());

app.MapControllers();
app.MapHub<RunHub>("/hubs/run");

app.Run();