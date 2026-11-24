using PriceTracker.Application;
using PriceTracker.Infrastracture;
using Microsoft.EntityFrameworkCore;
using Serilog;
using PriceTracker.Api.Extensions;
using PriceTracker.Api;

using PriceTracker.Api.Panel;

var createUser = args.Length > 0 && args[0] == "--create-panel-user";
if (createUser && args.Length != 3)
{
    Console.Error.WriteLine("Usage: --create-panel-user <username> <admin|viewer>");
    Environment.ExitCode = 1;
    return;
}
var builder = WebApplication.CreateBuilder(createUser ? [] : args);
var services = builder.Services;
var configuration = builder.Configuration;

//add serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration
    .Enrich.FromLogContext()
    .ReadFrom.Configuration(context.Configuration));

//register services
services.AddInfrastructureServices (configuration);
services.AddApplicationServices();
services.AddApiServices();
services.AddPanelSecurity();
services.AddHttpClient<PriceTracker.Domain.IServices.IProductSource, PriceTracker.Infrastracture.Services.SnappShopProductSource>(client =>
{
    client.BaseAddress = new Uri("https://apix.snappshop.ir/");
    client.MaxResponseContentBufferSize = 12_000_000;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

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

if (createUser)
{
    Environment.ExitCode = await PanelSecurity.CreateUserAsync(app.Services, args[1], args[2]);
    return;
}

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    context.Response.OnStarting(() =>
    {
        context.Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    });
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    var request = context.Request;
    if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)
        && !HttpMethods.IsOptions(request.Method) && request.Headers.TryGetValue("Origin", out var origin)
        && (!Uri.TryCreate(origin.ToString(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.GetLeftPart(UriPartial.Authority), $"{request.Scheme}://{request.Host}",
                StringComparison.OrdinalIgnoreCase)))
    {
        await PanelSecurity.Error(context, 403, "درخواست از مبدأ غیرمجاز است.");
        return;
    }
    await next();
});

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

app.UseRateLimiter();
app.UseAuthentication();

app.UseAuthorization();


app.MapControllers();

app.Run();
