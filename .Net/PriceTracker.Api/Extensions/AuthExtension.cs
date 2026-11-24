

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PriceTracker.Back.Models.Statics;

namespace FormBuilder.Extensions
{
    public static class AuthExtension
    {
        public static void AddAuthenticate(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer("Bearer", options =>
            {
                options.Authority = configuration["Api:SSOUrl"];

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false
                };
            });
        }

        public static void AddAuthPolicy(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                foreach (var scope in ClientScopes.GetScopes())
                {
                    options.AddPolicy(scope.Name, policy =>
                    {
                        policy.RequireClaim(ClientScopes.ScopeClaim, scope.Values);
                    });
                }
            });
        }
    }
}
