using Microsoft.IdentityModel.Tokens;
using Authentication.Fido2.Options;
using Authentication.Fido2.Data.Repositories.Interfaces;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;


namespace Authentication.Fido2.Extensions;


public static class AuthenticationExtensions
{
    public const string FullAccessScheme = JwtBearerDefaults.AuthenticationScheme;
    public const string MfaChallengeScheme = "MfaChallenge";
    public const string LoginEnrollmentScheme = "LoginEnrollment";
    
    [Obsolete("Use MfaChallengeScheme instead")]
    public const string MfaScheme = "MfaBearer";

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        var mfaJwtOptions = configuration.GetSection("MfaJwt").Get<MfaJwtOptions>() ?? new MfaJwtOptions();
        var loginEnrollmentJwtOptions =
            configuration.GetSection("LoginEnrollmentJwt").Get<LoginEnrollmentJwtOptions>()
            ?? new LoginEnrollmentJwtOptions();
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

                        if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(tokenJti))
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
            // MFA Challenge scheme: for MFA login challenge endpoints
            .AddJwtBearer(MfaChallengeScheme, options =>
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

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        // Validate token_type claim
                        var tokenType = context.Principal?.FindFirst("token_type")?.Value;
                        if (!string.Equals(tokenType, "mfa", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Fail("Token must be an MFA challenge token (token_type=mfa).");
                            return;
                        }

                        // Validate JTI in session
                        var tokenJti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                        if (string.IsNullOrWhiteSpace(tokenJti))
                        {
                            context.Fail("MFA token missing JTI claim.");
                            return;
                        }

                        var repository = context.HttpContext.RequestServices
                            .GetRequiredService<IMfaTempTokenSessionRepository>();
                        var tokenSession = await repository.GetActiveByJtiAsync(tokenJti, context.HttpContext.RequestAborted);

                        if (tokenSession is null)
                        {
                            context.Fail("MFA token session not found or expired.");
                        }
                    }
                };
            })
            // Login Enrollment scheme: for login-time MFA enrollment endpoints
            .AddJwtBearer(LoginEnrollmentScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = loginEnrollmentJwtOptions.Issuer,
                    ValidAudience = loginEnrollmentJwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(loginEnrollmentJwtOptions.SecretKey)
                    ),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        // Validate token_type claim
                        var tokenType = context.Principal?.FindFirst("token_type")?.Value;
                        if (!string.Equals(tokenType, "login_enrollment", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Fail("Token must be a login enrollment token (token_type=login_enrollment).");
                            return;
                        }

                        // Validate JTI in session
                        var tokenJti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                        if (string.IsNullOrWhiteSpace(tokenJti))
                        {
                            context.Fail("Login enrollment token missing JTI claim.");
                            return;
                        }

                        var repository = context.HttpContext.RequestServices
                            .GetRequiredService<IMfaLoginEnrollmentSessionRepository>();
                        var tokenSession = await repository.GetActiveByJtiAsync(tokenJti, context.HttpContext.RequestAborted);

                        if (tokenSession is null)
                        {
                            context.Fail("Login enrollment token session not found or expired.");
                        }
                    }
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

