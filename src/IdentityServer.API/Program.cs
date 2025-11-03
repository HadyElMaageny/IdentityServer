using IdentityServer.API.Extensions;
using IdentityServer.API.Filters;
using IdentityServer.API.Middleware;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting IdentityServer API");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
    });

    // Add custom service extensions
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddSwaggerDocumentation();
    builder.Services.AddApiVersioningConfiguration();
    builder.Services.AddCorsPolicy();
    builder.Services.AddHealthChecksConfiguration(builder.Configuration);

    // Add API Explorer
    builder.Services.AddEndpointsApiExplorer();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "IdentityServer API V1");
            c.RoutePrefix = string.Empty; // Serve Swagger UI at root
        });
    }

    // Use custom exception handling middleware
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseHttpsRedirection();

    // Use CORS
    app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "Production");

    // Use Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Use Serilog request logging
    app.UseSerilogRequestLogging();

    // Map controllers
    app.MapControllers();

    // Map health checks
    app.MapHealthChecks("/health");

    Log.Information("IdentityServer API started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
}
finally
{
    Log.CloseAndFlush();
}
