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

### SQL Server LocalDB Management (Windows)

```bash
# Start LocalDB instance
sqllocaldb start mssqllocaldb

# Stop LocalDB instance
sqllocaldb stop mssqllocaldb

# Check LocalDB instance info
sqllocaldb info mssqllocaldb

# List all LocalDB instances
sqllocaldb info

# Create new LocalDB instance
sqllocaldb create IdentityServerInstance

# Delete LocalDB instance (removes all data)
sqllocaldb delete IdentityServerInstance
```

### Process and Port Management (Windows)

```bash
# Check if port is in use
netstat -ano | findstr :5000
netstat -ano | findstr :7208

# Find process by PID
tasklist | findstr <PID>

# Kill process by PID
taskkill /PID <PID> /F

# Kill process by name
taskkill /IM dotnet.exe /F
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

### Current Domain Entities

The project currently implements the following entities in the Domain layer:

**Core Entities:**
- **BaseEntity**: Base class with Id (long), CreatedAt, UpdatedAt, IsDeleted, CreatedBy, UpdatedBy

**Identity & Authentication Entities:**
- **User**: System users with credentials (Username, Email, Password, IsActive)
  - Navigation: Tokens collection
- **Token**: JWT tokens and refresh tokens (linked to User)
- **Client**: OAuth/OIDC client applications with redirect URIs and secrets
  - Navigation: ClientScopes, UserConsents, AuthorizationCodes
- **Scope**: OAuth/OIDC authorization scopes
  - Navigation: ClientScopes
- **ClientScope**: Many-to-many join entity between Client and Scope
- **AuthorizationCode**: OAuth authorization codes for authorization code flow
  - Links User, Client with redirect URI, scopes, and expiration
- **UserConsent**: Tracks user consent grants for client applications
  - Records which scopes were granted to which client by which user

All entities (except ClientScope) inherit from `BaseEntity` and support soft deletes and audit tracking.

### Key Patterns Implemented

**Repository Pattern**: Abstracts data access logic
```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    IQueryable<T> Query();
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

## Authentication & Identity Implementation

The project includes a complete JWT-based authentication system with the following entities and services:

### Domain Entities

**User Entity** (`Domain/Entities/User.cs`)
- Username, Email, Password (hashed)
- IsActive flag
- Navigation property for Tokens

**Token Entity** (`Domain/Entities/Token.cs`)
- Stores refresh tokens and access tokens
- Linked to User entity

**Client Entity** (`Domain/Entities/Client.cs`)
- OAuth/OIDC client applications
- Client credentials and configurations

**Scope Entity** (`Domain/Entities/Scope.cs`)
- OAuth/OIDC scopes for authorization

### Authentication Services

**IAuthService** (`Application/Interfaces/IAuthService.cs`)
- `RegisterAsync(RegisterRequest)`: User registration
- `LoginAsync(LoginRequest)`: User authentication

**ITokenService** (`Application/Interfaces/ITokenService.cs`)
- `GenerateTokensAsync(User)`: JWT token generation (access + refresh tokens)

**ITokenEndpointService** (`Application/Interfaces/ITokenEndpointService.cs`)
- `ProcessTokenRequestAsync(TokenRequest)`: OAuth2 token endpoint handler
- Supports multiple grant types (authorization_code, refresh_token, etc.)
- Handles client authentication and validation

### Password Hashing

The project uses `Microsoft.AspNetCore.Identity.PasswordHasher<User>` for secure password hashing:
- Automatically applies PBKDF2 algorithm with salt
- Registered in `ServiceExtensions.AddApplicationServices()`
- Inject `IPasswordHasher<User>` in AuthService for password operations

### Authentication DTOs

- **RegisterRequest**: Username, Email, Password
- **LoginRequest**: Email/Username, Password
- **AuthResponse**: AccessToken, RefreshToken, ExpiresIn, TokenType

### JWT Configuration

JWT settings are configured in `appsettings.json` under `JwtSettings`:
```json
{
  "Secret": "YourSuperSecretKeyForJWTTokenGenerationThatIsAtLeast32CharactersLong",
  "Issuer": "https://localhost:7208",
  "Audience": "identityserver.clients",
  "AccessTokenMinutes": 30,
  "RefreshTokenDays": 30
}
```

The JWT authentication is registered in `ServiceExtensions.AddJwtAuthentication()` and applies to all endpoints marked with `[Authorize]` attribute.

**Important**:
- The `Secret` must be at least 32 characters long for HS256
- Change `Issuer` to match your production URL in production
- Store secrets in environment variables or Azure Key Vault for production

### OAuth/OIDC Support

The domain model supports OAuth 2.0 and OpenID Connect flows:

**Authorization Code Flow** (via `AuthorizationCode` entity):
- Stores authorization codes with expiration
- Links user consent to specific clients and scopes
- Tracks redirect URIs for security

**User Consent Management** (via `UserConsent` entity):
- Records which scopes users have granted to clients
- Enables remember consent functionality
- Audit trail of consent grants

**Scope-based Authorization** (via `Scope` and `ClientScope` entities):
- Define available scopes (openid, profile, email, etc.)
- Control which scopes each client can request
- Many-to-many relationship between clients and scopes

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

### Example: Adding a "Resource" Entity (for OAuth Resource Servers)

**Domain Layer:**
```csharp
// Domain/Entities/Resource.cs
public class Resource : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    // Navigation
    public ICollection<ResourceScope> ResourceScopes { get; set; } = new List<ResourceScope>();
}
```

**Application Layer:**
```csharp
// Application/DTOs/ResourceDto.cs
public class ResourceDto
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public bool Enabled { get; set; }
}

// Application/Interfaces/IResourceService.cs
public interface IResourceService
{
    Task<Result<ResourceDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<ResourceDto>>> GetAllAsync(CancellationToken cancellationToken = default);
    // ... other methods
}
```

**Infrastructure Layer:**
```csharp
// Infrastructure/Configurations/ResourceConfiguration.cs
public class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resources");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Description).HasMaxLength(1000);

        // Index for lookups
        builder.HasIndex(r => r.Name).IsUnique();
    }
}

// Add to ApplicationDbContext.cs OnModelCreating:
modelBuilder.ApplyConfiguration(new ResourceConfiguration());
```

**Register service in API/Extensions/ServiceExtensions.cs:**
```csharp
services.AddScoped<IResourceService, ResourceService>();
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

## .NET Best Practices & Coding Standards

### C# Language Features
- **Use C# 10+ features** when appropriate:
  - Record types for immutable DTOs
  - Pattern matching for cleaner conditional logic
  - Null-coalescing assignment (`??=`)
  - File-scoped namespaces
  - Global using directives
- **Async/await**: Use for all I/O-bound operations (database, HTTP calls)
- **LINQ and Lambda expressions**: Prefer for collection operations
- **String interpolation**: Use `$"{variable}"` over `String.Format()`

### Naming Conventions
- **PascalCase**: Class names, method names, public members
- **camelCase**: Local variables, private fields
- **UPPERCASE**: Constants
- **Prefix interfaces with "I"**: `IUserService`, `IRepository<T>`

### Error Handling
- **Use exceptions for exceptional cases**, not for control flow
- **Implement global exception handling middleware** (already in place)
- **Log exceptions properly** with structured logging (Serilog)
- **Return appropriate HTTP status codes**: 200, 201, 400, 401, 403, 404, 500
- **Use Result pattern** for business logic errors (avoid exceptions for validation)

### Performance & Optimization
- **Async/await**: Always use `async`/`await` for database and HTTP operations
- **Avoid N+1 queries**: Use `.Include()` for eager loading in EF Core
- **Implement pagination**: For endpoints returning large datasets
- **Use `CancellationToken`**: Pass through all async methods for request cancellation
- **Efficient LINQ**: Use `Any()` instead of `Count() > 0`, `FirstOrDefault()` instead of `Where().First()`

### Testing (Future Implementation)
- **Unit Testing**: xUnit, NUnit, or MSTest
- **Mocking**: Use Moq or NSubstitute for mocking dependencies
- **Integration Tests**: Test API endpoints end-to-end
- **Test Structure**: Arrange-Act-Assert pattern

### Security Best Practices
- **Never log sensitive data**: Passwords, tokens, secrets
- **Use parameterized queries**: EF Core does this by default
- **Validate all inputs**: FluentValidation for DTOs
- **Use `[Authorize]` attribute**: Protect endpoints requiring authentication
- **HTTPS only**: Enforce SSL in production
- **CORS policies**: Configure specific origins, not `AllowAnyOrigin()` in production

## Key Technologies

### Dependencies
- **Entity Framework Core 9.x**: ORM for data access
- **AutoMapper 15.x**: Object-object mapping
- **FluentValidation 12.x**: Input validation
- **MediatR 13.x**: CQRS pattern support (ready to use)
- **Serilog 9.x**: Structured logging
- **Swashbuckle 6.x**: Swagger/OpenAPI documentation
- **JWT Bearer Authentication**: Token-based authentication
- **ASP.NET Core Identity PasswordHasher**: Secure password hashing (PBKDF2)

### Design Decisions

**Why Long for IDs?**
- Better database performance with clustered indexes
- Sequential IDs enable better query optimization
- More compact than GUIDs (8 bytes vs 16 bytes)
- Easier to work with in URLs and debugging

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

## Current API Endpoints

The IdentityServer currently implements the following endpoints:

### Authentication API (v1)

**Base Route:** `/api/v1/auth`

| Method | Endpoint | Description | Request Body | Response |
|--------|----------|-------------|--------------|----------|
| POST | `/api/v1/auth/register` | Register new user | `RegisterRequest` | `Result<AuthResponse>` |
| POST | `/api/v1/auth/login` | Authenticate user | `LoginRequest` | `Result<AuthResponse>` |

**RegisterRequest:**
```json
{
  "username": "string",
  "email": "string",
  "password": "string"
}
```

**LoginRequest:**
```json
{
  "email": "string",
  "password": "string"
}
```

**AuthResponse:**
```json
{
  "accessToken": "string",
  "refreshToken": "string",
  "expiresIn": 1800,
  "tokenType": "Bearer"
}
```

### OAuth2 Token Endpoint

**Base Route:** `/connect`

| Method | Endpoint | Description | Content-Type | Response |
|--------|----------|-------------|--------------|----------|
| POST | `/connect/token` | OAuth2 token endpoint (RFC 6749) | `application/x-www-form-urlencoded` | `TokenResponse` or `TokenErrorResponse` |

**Supported Grant Types:**
- `authorization_code` - Exchange authorization code for tokens
- `refresh_token` - Refresh an access token
- Additional grant types as implemented

**Token Request (form-encoded):**
```
grant_type=authorization_code
&code=AUTH_CODE_HERE
&client_id=CLIENT_ID
&client_secret=CLIENT_SECRET
&redirect_uri=REDIRECT_URI
```

**Token Response:**
```json
{
  "access_token": "string",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "string"
}
```

**Error Response (RFC 6749 Section 5.2):**
```json
{
  "error": "invalid_grant",
  "error_description": "Authorization code is invalid or expired"
}
```

### Health Check Endpoint

| Method | Endpoint | Description | Response |
|--------|----------|-------------|----------|
| GET | `/health` | Application health status | `200 OK` or `503 Service Unavailable` |

The health check endpoint verifies:
- Application is running
- Database connectivity (`ApplicationDbContext` check)

### Example: Testing with cURL

**Register a new user:**
```bash
curl -X POST https://localhost:7208/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "SecurePassword123!"
  }'
```

**Login:**
```bash
curl -X POST https://localhost:7208/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePassword123!"
  }'
```

**OAuth2 Token Request:**
```bash
curl -X POST https://localhost:7208/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=authorization_code&code=AUTH_CODE&client_id=CLIENT_ID&client_secret=CLIENT_SECRET&redirect_uri=https://client.example.com/callback"
```

## Common Tasks

### Adding Custom Health Checks

The application uses ASP.NET Core Health Checks. To add custom checks:

**1. Create a custom health check class:**
```csharp
// Infrastructure/HealthChecks/ExternalApiHealthCheck.cs
public class ExternalApiHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;

    public ExternalApiHealthCheck(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("https://api.example.com/health", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("External API is responsive");
            }

            return HealthCheckResult.Degraded($"External API returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("External API is unreachable", ex);
        }
    }
}
```

**2. Register in ServiceExtensions.cs:**
```csharp
services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database")
    .AddCheck<ExternalApiHealthCheck>("external-api");
```

**3. Add detailed health check UI (optional):**
```bash
# Install package
dotnet add package AspNetCore.HealthChecks.UI.Client

# In Program.cs, update the MapHealthChecks:
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

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
    public static async Task SeedDataAsync(ApplicationDbContext context)
    {
        // Seed default scopes
        if (!await context.Scopes.AnyAsync())
        {
            context.Scopes.AddRange(
                new Scope { Name = "openid", Description = "OpenID Connect scope" },
                new Scope { Name = "profile", Description = "User profile scope" },
                new Scope { Name = "email", Description = "Email scope" }
            );
            await context.SaveChangesAsync();
        }
    }
}

// Call from Program.cs after app.Build():
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DataSeeder.SeedDataAsync(context);
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
- For LocalDB on Windows:
  - Start instance: `sqllocaldb start mssqllocaldb`
  - Check status: `sqllocaldb info mssqllocaldb`
  - If instance doesn't exist: `sqllocaldb create mssqllocaldb`
- Check if database exists: `dotnet ef database update --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API`

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

1. **Password Hashing**: ✅ Already implemented
   - Uses `PasswordHasher<User>` with PBKDF2 algorithm
   - Automatically salts passwords
   - Configured in `ServiceExtensions.AddApplicationServices()`

2. **JWT Secret**: Change the default JWT secret in production
   - Store in environment variables or Azure Key Vault
   - Use a strong, randomly generated secret (at least 32 characters)

3. **Connection Strings**: Never commit connection strings with production credentials
   - Use User Secrets for development
   - Use Azure Key Vault or AWS Secrets Manager for production

4. **CORS**: Update CORS policy for production
   - Replace "AllowAll" with specific origins
   - Don't use `AllowAnyOrigin()` in production

5. **HTTPS**: Always enforce HTTPS in production
   - Configure HSTS (HTTP Strict Transport Security)
   - Use valid SSL certificates

## Additional Resources

- [Clean Architecture Guide](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Repository Pattern](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)
- [EF Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
