using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Application.Behaviors;
using PriceTracker.Application.Services;
using PriceTracker.Application.Services.Shared;
using MediatR;
using FluentValidation;

namespace PriceTracker.Application
{
    public static class AppRegisterServices
    {
        public static void AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<ICurrentUser, CurrentUser>();

            //mediatr
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(AppRegisterServices).Assembly));

            //validation
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            services.AddValidatorsFromAssembly(typeof(AppRegisterServices).Assembly);


            
            //mapper
            services.AddAutoMapper(typeof(AppRegisterServices).Assembly);
        }
    }
}
