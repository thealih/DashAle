using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceTracker.Domain.IRepositories.Common;
using PriceTracker.Infrastracture.Interceptors;
using PriceTracker.Infrastracture.Repositories.Common;

namespace PriceTracker.Infrastracture
{
    public static class RegisterServices
    {
        public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            //db
            var connectionString = configuration["DB:SqlConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Set DB__SqlConnectionString to connect to SQL Server.");
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



            //register services
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();


        }
    }
}
