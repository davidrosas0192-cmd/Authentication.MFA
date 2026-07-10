using Microsoft.IdentityModel.Tokens;
using Authentication.Fido2.Options;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;


namespace Authentication.Fido2.Extensions;


public static class AuthenticationExtensions
{
    public const string FullAccessScheme = JwtBearerDefaults.AuthenticationScheme;
    public const string MfaScheme = "MfaBearer";

    public static  IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        var mfaJwtOptions = configuration.GetSection("MfaJwt").Get<MfaJwtOptions>() ?? new MfaJwtOptions();
        var fido2Options = configuration.GetSection("Fido2").Get<Fido2Options>() ?? new Fido2Options();

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = FullAccessScheme;
                options.DefaultChallengeScheme = FullAccessScheme;
            })
            .AddJwtBearer(FullAccessScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            })
            .AddJwtBearer(MfaScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = mfaJwtOptions.Issuer,
                    ValidAudience = mfaJwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(mfaJwtOptions.SecretKey)
                    ),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddFido2(options => 
        {
            options.ServerName = fido2Options.ServerName!;
            options.ServerDomain = fido2Options.ServerDomain!;
            options.Origins = fido2Options.Origins!;
        });

        return services;
    }    
}

