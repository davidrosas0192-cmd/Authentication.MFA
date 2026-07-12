using Microsoft.IdentityModel.Tokens;
using Authentication.Fido2.Options;
using Authentication.Fido2.Data.Repositories.Interfaces;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Authentication.Fido2.Constants;


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

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userIdValue = context.Principal?.FindFirst("sub")?.Value
                            ?? context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                            ?? context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var tokenJti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                        if (!long.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(tokenJti))
                        {
                            context.Fail("Invalid token claims.");
                            return;
                        }

                        var repository = context.HttpContext.RequestServices.GetRequiredService<IAccessTokenSessionRepository>();
                        var tokenSession = await repository.GetActiveByJtiAsync(tokenJti, context.HttpContext.RequestAborted);

                        if (tokenSession is null || tokenSession.UserId != userId)
                        {
                            context.Fail("Token session is no longer valid.");
                        }
                    },
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

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRoles.Admin));
            options.AddPolicy("SupportOrAdmin", policy => policy.RequireRole(UserRoles.Support, UserRoles.Admin));
            options.AddPolicy(
                "UserSupportAdmin",
                policy => policy.RequireRole(UserRoles.User, UserRoles.Support, UserRoles.Admin)
            );
        });

        return services;
    }    
}

