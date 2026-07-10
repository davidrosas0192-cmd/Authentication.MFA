using Authentication.Fido2.Services.Interfaces;
using Authentication.Fido2.Services.Implementations;
using Authentication.Fido2.Data.Repositories.Interfaces;
using Authentication.Fido2.Data.Repositories.Implementations;
using Authentication.Fido2.Options;

namespace Authentication.Fido2.Extensions;


public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register your application services here
        // Example: services.AddScoped<IYourService, YourServiceImplementation>();
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<Fido2Options>(configuration.GetSection("Fido2"));
        services.AddHttpContextAccessor();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFido2MfaService, Fido2MfaService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFido2CredentialRepository, Fido2CredentialRepository>();
        services.AddScoped<IFido2TransactionRepository, Fido2TransactionRepository>();
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}