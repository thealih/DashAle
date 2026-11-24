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
        public static void AddApiServices(this IServiceCollection services)
        {
            //versioning
            services.AddApiVersioning(
                options =>
                {
                    options.ReportApiVersions = true;
                })
            .AddMvc()
            .AddApiExplorer(
                options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

            //authentication
            services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();


            // Add services to the container.
            services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            services.AddEndpointsApiExplorer();
            services.AddSwagger();
            services.AddHttpContextAccessor();



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
