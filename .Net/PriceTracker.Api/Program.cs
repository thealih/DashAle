using PriceTracker.Application;
using PriceTracker.Infrastracture;
using Microsoft.EntityFrameworkCore;
using Serilog;
using PriceTracker.Api.Extensions;
using PriceTracker.Api;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

//add serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration
    .Enrich.FromLogContext()
    .ReadFrom.Configuration(context.Configuration));

//register services
services.AddInfrastructureServices (configuration);
services.AddApplicationServices(configuration);
services.AddApiServices(configuration);

//cors
const string spaCorsPolicyName = "reactApp";

services.AddCors(config =>
{
    config.AddPolicy(spaCorsPolicyName, options =>
    {
        // TODO : Add origins array
        options.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
    });
});

var app = builder.Build();

//migrate db
using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI(
options =>
{
    var descriptions = app.DescribeApiVersions(); foreach (var description in descriptions)
    {
        options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
    }
});
//}

app.UseHttpsRedirection();

app.UseSerilogRequestLogging();

app.UseRouting();

app.UseExceptionHandler();

app.UseCors(spaCorsPolicyName);

app.UseAuthentication();

app.UseAuthorization();

app.UsePersianNumbersMiddleware();

app.MapControllers();

app.Run();
