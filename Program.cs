using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using zListBack.Data;
using zListBack.Extensions;
using zListBack.Repositories;
using zListBack.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddApplicationServices(builder.Configuration);

// Add services to the container.
builder.Services.AddScoped<EmailService>();


// Configure JWT Authentication
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
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = authHeader.Substring("Bearer ".Length).Trim();
                }

                Console.WriteLine($"Processed Token: {context.Token}");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                if (context.Exception is SecurityTokenExpiredException)
                {
                    Console.WriteLine("❌ Token has expired!");
                }
                else if (context.Exception is SecurityTokenInvalidSignatureException)
                {
                    Console.WriteLine("❌ Token signature is invalid!");
                }
                else if (context.Exception is SecurityTokenInvalidIssuerException)
                {
                    Console.WriteLine("❌ Invalid token issuer!");
                }
                else if (context.Exception is SecurityTokenInvalidAudienceException)
                {
                    Console.WriteLine("❌ Invalid token audience!");
                }
                else
                {
                    Console.WriteLine($"Unknown token error: {context.Exception.GetType()}");
                }
                return Task.CompletedTask;
            }
        };
    });


//builder.Services.AddScoped<UserRepository>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

app.Use(async (context, next) =>
{
    Console.WriteLine($"Authorization Header: {context.Request.Headers["Authorization"]}");
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
