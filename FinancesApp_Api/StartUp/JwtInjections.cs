    using FinancesApp_Api.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace FinancesApp_Api.StartUp;

public static class JwtInjections
{
    public static IServiceCollection AddJwtServices(this IServiceCollection services, IConfigurationManager configuration)
    {

        services.AddSingleton<JwtHandler>();
        services.AddSingleton<JwtClaimsDecryptor>();

        services.AddHttpClient<JwtService>(client =>
        {
            client.BaseAddress = new Uri(configuration["IdentityApi:BaseAddress"] ?? "http://localhost:5002");
        });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {

            var jwtHandler = new JwtHandler(configuration);

            options.TokenValidationParameters = jwtHandler.GetTokenValidationParameters();

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Cookies.ContainsKey("X-Access-Token"))
                        context.Token = context.Request.Cookies["X-Access-Token"];
                    
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    Console.WriteLine($"Token validated for: {context.Principal?.Identity?.Name}");
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        return services;
    }
}
