# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a professional .NET 8 Web API built with **Clean Architecture** (Onion Architecture) principles. The solution provides a complete backend infrastructure with CRUD operations, authentication structure, validation, logging, and follows industry best practices.

## Solution Structure

The solution follows Clean Architecture with clear separation of concerns across multiple projects:

### Projects

1. **IdentityServer.API** - Presentation Layer (ASP.NET Core Web API)
   - Controllers, Middleware, Filters
   - Dependency injection configuration
   - Entry point (Program.cs)

2. **IdentityServer.Application** - Application Layer (Business Logic)
   - DTOs, Service Interfaces, Service Implementations
   - AutoMapper mappings, FluentValidation validators
   - Depends on: Domain, Shared

3. **IdentityServer.Domain** - Domain Layer (Core Business Rules)
   - Entities, Value Objects, Domain Exceptions
   - Repository interfaces (IRepository, IUnitOfWork)
   - No dependencies on other projects

4. **IdentityServer.Infrastructure** - Infrastructure Layer (Data Access)
   - EF Core DbContext, Repository implementations
   - Entity configurations, Migrations
   - Depends on: Domain, Application

5. **IdentityServer.Shared** - Shared Layer (Cross-cutting Concerns)
   - Result pattern, Pagination models
   - Common utilities
   - No dependencies on other projects

### Dependency Flow

```
API → Application → Domain
API → Infrastructure → Application → Domain
API → Shared
Application → Shared
```

## Build and Development Commands

### Building the Solution

```bash
# Build the entire solution
dotnet build IdentityServer.sln

# Build in Release mode
dotnet build IdentityServer.sln -c Release

# Restore NuGet packages
dotnet restore IdentityServer.sln

# Clean build artifacts
dotnet clean IdentityServer.sln
```

### Running the Application

```bash
# Run the API project
dotnet run --project src/IdentityServer.API/IdentityServer.API.csproj

# Run with specific environment
dotnet run --project src/IdentityServer.API/IdentityServer.API.csproj --environment Development

# Watch mode (auto-reload on changes)
dotnet watch run --project src/IdentityServer.API/IdentityServer.API.csproj
```

### Database Operations (Entity Framework Core)

```bash
# Add a new migration (from solution root)
dotnet ef migrations add <MigrationName> --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API

# Update database
dotnet ef database update --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API

# Remove last migration
dotnet ef migrations remove --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API

# Generate SQL script for migration
dotnet ef migrations script --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API

# Drop database (use with caution!)
dotnet ef database drop --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API
```

## Architecture Principles

### Clean Architecture Layers

**Domain Layer (Core)**
- Contains enterprise business rules
- No dependencies on other projects
- Entities inherit from BaseEntity (Id, CreatedAt, UpdatedAt, IsDeleted, etc.)
- Repository interfaces (IRepository<T>, IUnitOfWork)

**Application Layer**
- Contains application business rules
- Defines DTOs for data transfer
- Service interfaces and implementations
- AutoMapper profiles for entity-DTO mapping
- FluentValidation validators for input validation
- Depends only on Domain and Shared

**Infrastructure Layer**
- Implements data access with EF Core
- Repository pattern implementation
- Unit of Work implementation
- Entity configurations (Fluent API)
- Database migrations
- External service integrations

**Presentation Layer (API)**
- ASP.NET Core Web API controllers
- Middleware for exception handling
- Action filters for validation
- Service registration and DI configuration
- Swagger/OpenAPI documentation

### Key Patterns Implemented

**Repository Pattern**: Abstracts data access logic
```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    // ... other methods
}
```

**Unit of Work Pattern**: Manages database transactions
```csharp
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
```

**Result Pattern**: Consistent API responses
```csharp
public class Result<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
}
```

## Adding New Features

### Adding a New Entity

1. Create entity in `Domain/Entities/` inheriting from `BaseEntity`
2. Create DTOs in `Application/DTOs/` (CreateDto, UpdateDto, ResponseDto)
3. Create service interface in `Application/Interfaces/`
4. Implement service in `Application/Services/`
5. Add AutoMapper mappings in `Application/Mappings/MappingProfile.cs`
6. Create FluentValidation validators in `Application/Validators/`
7. Create entity configuration in `Infrastructure/Configurations/`
8. Add DbSet to `ApplicationDbContext`
9. Create migration: `dotnet ef migrations add Add<EntityName>`
10. Create controller in `API/Controllers/`

### Adding a New API Endpoint

1. Add method to service interface in `Application/Interfaces/`
2. Implement method in service class in `Application/Services/`
3. Add controller action in `API/Controllers/`
4. Add XML documentation comments for Swagger
5. Test using Swagger UI or Postman

### Example: Adding a "Category" Entity

**Domain Layer:**
```csharp
// Domain/Entities/Category.cs
public class Category : BaseEntity
{
    public string Name { get; set; }
    public string Description { get; set; }
}
```

**Application Layer:**
```csharp
// Application/DTOs/CategoryDto.cs
public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}

// Application/Interfaces/ICategoryService.cs
public interface ICategoryService
{
    Task<Result<CategoryDto>> GetByIdAsync(Guid id);
    // ... other methods
}
```

**Infrastructure Layer:**
```csharp
// Infrastructure/Configurations/CategoryConfiguration.cs
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
    }
}
```

**Register service in API/Extensions/ServiceExtensions.cs:**
```csharp
services.AddScoped<ICategoryService, CategoryService>();
```

## Configuration Files

### appsettings.json
- **ConnectionStrings**: Database connection string
- **JwtSettings**: JWT token configuration (Secret, Issuer, Audience, Expiration)
- **Serilog**: Logging configuration
- **AllowedHosts**: Allowed host names

### appsettings.Development.json
- Development-specific overrides
- More verbose logging
- Development database connection string

## Key Technologies

### Dependencies
- **Entity Framework Core 9.x**: ORM for data access
- **AutoMapper 15.x**: Object-object mapping
- **FluentValidation 12.x**: Input validation
- **MediatR 13.x**: CQRS pattern support (ready to use)
- **Serilog 9.x**: Structured logging
- **Swashbuckle 6.x**: Swagger/OpenAPI documentation
- **JWT Bearer Authentication**: Token-based authentication

### Design Decisions

**Why Guid for IDs?**
- Better for distributed systems
- More secure than sequential integers
- Prevents ID enumeration attacks

**Why Soft Deletes?**
- Data recovery capability
- Audit trail preservation
- Referential integrity maintenance

**Why Result Pattern?**
- Consistent API responses
- Explicit error handling
- Type-safe success/failure handling

**Why Repository Pattern?**
- Abstraction of data access logic
- Easier to test (mockable)
- Flexibility to change data source

## Common Tasks

### Running Migrations on Startup

Add to `Program.cs`:
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}
```

### Seeding Data

Create a `DataSeeder` class in Infrastructure:
```csharp
public static class DataSeeder
{
    public static void SeedData(ApplicationDbContext context)
    {
        if (!context.Products.Any())
        {
            context.Products.AddRange(/* seed data */);
            context.SaveChanges();
        }
    }
}
```

### Adding Authentication to Endpoints

```csharp
[Authorize] // Requires authentication
[ApiController]
public class SecureController : ControllerBase
{
    [AllowAnonymous] // Override for specific action
    public IActionResult PublicAction() { }
}
```

## Testing

### Manual Testing
- Use Swagger UI at the root URL (Development environment)
- Use Postman with the OpenAPI spec import
- Use the `/health` endpoint to verify the application is running

### Future Unit Testing Structure
```
tests/
├── IdentityServer.Application.Tests/
├── IdentityServer.Domain.Tests/
└── IdentityServer.API.IntegrationTests/
```

## Troubleshooting

### Common Issues

**Migration Error: "Build failed"**
- Ensure all projects compile: `dotnet build IdentityServer.sln`
- Check for syntax errors in entity classes

**Database Connection Error**
- Verify connection string in appsettings.json
- Ensure SQL Server is running
- For LocalDB: `sqllocaldb start mssqllocaldb`

**Swagger Not Loading**
- Only available in Development environment
- Check that API is running on correct port
- Verify XML documentation generation is enabled

**Validation Not Working**
- Ensure validators are registered: `services.AddValidatorsFromAssembly()`
- Check validator class name follows convention: `<DtoName>Validator`
- Verify FluentValidation is configured in Program.cs

## Security Notes

⚠️ **Important for Production:**

1. **JWT Secret**: Change the default JWT secret in production
   - Store in environment variables or Azure Key Vault
   - Use a strong, randomly generated secret (at least 32 characters)

2. **Connection Strings**: Never commit connection strings with production credentials
   - Use User Secrets for development
   - Use Azure Key Vault or AWS Secrets Manager for production

3. **CORS**: Update CORS policy for production
   - Replace "AllowAll" with specific origins
   - Don't use `AllowAnyOrigin()` in production

4. **HTTPS**: Always enforce HTTPS in production
   - Configure HSTS (HTTP Strict Transport Security)
   - Use valid SSL certificates

## Additional Resources

- [Clean Architecture Guide](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Repository Pattern](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)
- [EF Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
