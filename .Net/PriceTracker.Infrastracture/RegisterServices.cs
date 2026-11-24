using Bill.Infrastracture.Models.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceTracker.Domain.IRepositories.Common;
using PriceTracker.Domain.IServices;
using PriceTracker.Infrastracture.Interceptors;
using PriceTracker.Infrastracture.Models.Options;
using PriceTracker.Infrastracture.Repositories.Common;
using PriceTracker.Infrastracture.Services;
using PriceTracker.Infrastracture.Services.Shared;

namespace PriceTracker.Infrastracture
{
    public static class RegisterServices
    {
        public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            //db
            string connectionString = configuration.GetValue<string>("DB:SqlConnectionString");
            services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            {
                var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

                IInterceptor[] interceptors =
                    [
                        new AuditableInterceptor(httpContextAccessor),
                        new PersianYeKeCommandInterceptor()
                    ];

                options.AddInterceptors(interceptors);

                options.UseSqlServer(connectionString);
            });

            //mongo
            services.Configure<MongoDbOptions>(configuration.GetSection("MongoDb"));

            //configuration
            services.Configure<ApiOptions>(configuration.GetSection("Api"));

            //register services
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            //externalServices
            services.AddScoped<IExSsoService, ExSsoService>();
            services.AddScoped<IExternalService, ExternalService>();

        }
    }
}
