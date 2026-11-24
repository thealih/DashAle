using PriceTracker.Application;
using PriceTracker.Infrastracture;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;
using PriceTracker.Api.Extensions;
using PriceTracker.Api;

using PriceTracker.Api.Panel;
using PriceTracker.Api.BackgroundServices;
using PriceTracker.Application.Features.Monitoring;
using PriceTracker.Domain.IServices;
using PriceTracker.Infrastracture.Services;

var createUser = args.Length > 0 && args[0] == "--create-panel-user";
var backup = args.Length > 0 && args[0] == "--backup-panel-db";
if (createUser && args.Length != 3)
{
    Console.Error.WriteLine("Usage: --create-panel-user <username> <admin|viewer>");
    Environment.ExitCode = 1;
    return;
}
if (backup && args.Length != 2)
{
    Console.Error.WriteLine("Usage: --backup-panel-db <destination.bak>");
    Environment.ExitCode = 1;
    return;
}
var builder = WebApplication.CreateBuilder(createUser || backup ? [] : args);
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
if (!createUser && !backup)
    services.AddPanelSecurity(configuration, builder.Environment.IsDevelopment());
services.AddHttpClient("SnappShop", client =>
{
    client.BaseAddress = new Uri("https://apix.snappshop.ir/");
    client.MaxResponseContentBufferSize = 12_000_000;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
services.AddSingleton(provider => new SnappShopProductSource(
    provider.GetRequiredService<IHttpClientFactory>().CreateClient("SnappShop")));
services.AddSingleton<IProductSource>(provider => provider.GetRequiredService<SnappShopProductSource>());
services.AddSingleton<IBrandProductSource>(provider => provider.GetRequiredService<SnappShopProductSource>());
services.AddScoped<MonitoringWorker>();
if (!createUser && !backup && configuration.GetValue<bool>("Monitoring:Enabled"))
    services.AddHostedService<MonitoringHostedService>();

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

if (backup)
{
    await using var scope = app.Services.CreateAsyncScope();
    await LocalDatabaseBackup.WriteAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), args[1]);
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
var panelPath = configuration["Panel:StaticPath"];
if (!string.IsNullOrWhiteSpace(panelPath))
{
    var directory = Path.GetFullPath(panelPath, app.Environment.ContentRootPath);
    if (!File.Exists(Path.Combine(directory, "index.html")))
        throw new InvalidOperationException("Panel:StaticPath must point to a built frontend containing index.html.");
    var files = new PhysicalFileProvider(directory);
    app.Lifetime.ApplicationStopped.Register(files.Dispose);
    app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"), branch =>
    {
        branch.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        branch.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = context =>
            {
                if (context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    context.Context.Response.Headers.CacheControl = "no-cache";
                context.Context.Response.Headers.XContentTypeOptions = "nosniff";
            }
        });
    });
}

app.UseRouting();

app.UseExceptionHandler();

app.UseCors(spaCorsPolicyName);

app.UseRateLimiter();
app.UseAuthentication();

app.UseAuthorization();


app.MapControllers();

app.Run();
