using FormBuilder.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using PriceTracker.Application.Middlewares;
using PriceTracker.Back.Extensions;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Api
{
    public static class RegisterServices
    {
        public static void AddApiServices(this IServiceCollection services, IConfiguration configuration)
        {
            //versioning
            services.AddApiVersioning(
                options =>
                {
                    options.ReportApiVersions = true;
                })
            .AddApiExplorer(
                options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

            //authentication
            services.AddAuthenticate(configuration);
            services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();


            //authorization
            services.AddAuthPolicy();

            services.AddEndpointsApiExplorer();

            // Add services to the container.
            services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            services.AddEndpointsApiExplorer();
            services.AddSwagger();
            services.AddHttpContextAccessor();

            //mapper
            services.AddAutoMapper(typeof(RegisterServices).Assembly);


            //exception handler
            services.AddExceptionHandler<ExceptionHandlingMiddleware>();
            services.AddProblemDetails();


            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });
        }
    }
}
