# IdentityServer - Clean Architecture .NET 8 API

A professional, production-ready ASP.NET Core Web API built with Clean Architecture (Onion Architecture) principles, featuring a complete CRUD implementation, authentication, validation, and comprehensive logging.

## 🏗️ Architecture

This project follows **Clean Architecture** (also known as Onion Architecture) principles, which provide:

- **Independence of Frameworks**: The architecture doesn't depend on external libraries
- **Testability**: Business rules can be tested without UI, database, or external elements
- **Independence of UI**: The UI can change without changing the rest of the system
- **Independence of Database**: Business rules are not bound to the database
- **Independence of External Agency**: Business rules don't know anything about external interfaces

### Project Structure

```
IdentityServer/
├── src/
│   ├── IdentityServer.API/              # Presentation Layer
│   │   ├── Controllers/                 # API Controllers
│   │   ├── Middleware/                  # Custom middleware
│   │   ├── Filters/                     # Action filters
│   │   ├── Extensions/                  # Service configuration extensions
│   │   ├── Program.cs                   # Application entry point
│   │   └── appsettings.json             # Configuration files
│   │
│   ├── IdentityServer.Application/      # Application Layer
│   │   ├── DTOs/                        # Data Transfer Objects
│   │   ├── Interfaces/                  # Service interfaces
│   │   ├── Services/                    # Business logic services
│   │   ├── Mappings/                    # AutoMapper profiles
│   │   └── Validators/                  # FluentValidation validators
│   │
│   ├── IdentityServer.Domain/           # Domain Layer (Core)
│   │   ├── Entities/                    # Domain entities
│   │   ├── ValueObjects/                # Value objects
│   │   ├── Enums/                       # Enumerations
│   │   ├── Exceptions/                  # Domain exceptions
│   │   └── Interfaces/                  # Repository interfaces
│   │
│   ├── IdentityServer.Infrastructure/   # Infrastructure Layer
│   │   ├── Data/                        # DbContext & UnitOfWork
│   │   ├── Repositories/                # Repository implementations
│   │   ├── Configurations/              # EF Core configurations
│   │   └── Services/                    # External service implementations
│   │
│   └── IdentityServer.Shared/           # Shared Layer
│       ├── Common/                      # Common utilities (Result pattern)
│       └── Models/                      # Shared models (Pagination)
│
└── IdentityServer.sln                   # Solution file
```

## 🚀 Features

### Core Features
- ✅ **Clean Architecture** with clear separation of concerns
- ✅ **Repository Pattern** with Unit of Work
- ✅ **Result Pattern** for consistent API responses
- ✅ **CQRS Ready** with MediatR support
- ✅ **Pagination Support** for list endpoints
- ✅ **Soft Delete** implementation
- ✅ **Audit Fields** (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)

### Technical Features
- ✅ **Entity Framework Core** with SQL Server
- ✅ **AutoMapper** for object mapping
- ✅ **FluentValidation** for input validation
- ✅ **Serilog** for structured logging
- ✅ **Swagger/OpenAPI** with JWT authentication support
- ✅ **JWT Authentication** structure ready
- ✅ **API Versioning** configured
- ✅ **CORS Policy** setup
- ✅ **Global Exception Handling** middleware
- ✅ **Health Checks** endpoint

## 📋 Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/sql-server) or SQL Server LocalDB
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) or [Rider](https://www.jetbrains.com/rider/)

## 🛠️ Getting Started

### 1. Clone the Repository

```bash
git clone <your-repo-url>
cd IdentityServer
```

### 2. Configure Database Connection

Update the connection string in `src/IdentityServer.API/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=IdentityServerDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

### 3. Apply Database Migrations

```bash
# Navigate to the Infrastructure project directory
cd src/IdentityServer.Infrastructure

# Add initial migration
dotnet ef migrations add InitialCreate --startup-project ../IdentityServer.API

# Update the database
dotnet ef database update --startup-project ../IdentityServer.API
```

### 4. Run the Application

```bash
# Navigate to the API project
cd ../IdentityServer.API

# Run the application
dotnet run
```

The API will be available at:
- HTTPS: `https://localhost:7xxx`
- HTTP: `http://localhost:5xxx`
- Swagger UI: `https://localhost:7xxx/` (root URL in Development)

## 📚 API Endpoints

### Products API (v1)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/products` | Get all products (paginated) |
| GET | `/api/v1/products/{id}` | Get product by ID |
| POST | `/api/v1/products` | Create new product |
| PUT | `/api/v1/products/{id}` | Update existing product |
| DELETE | `/api/v1/products/{id}` | Delete product (soft delete) |

### Health Check

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Application health check |

### Example Request

**Create Product:**
```bash
curl -X POST https://localhost:7xxx/api/v1/products \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Sample Product",
    "description": "This is a sample product",
    "price": 29.99,
    "stock": 100,
    "category": "Electronics",
    "isActive": true
  }'
```

**Get Products with Pagination:**
```bash
curl -X GET "https://localhost:7xxx/api/v1/products?pageNumber=1&pageSize=10"
```

## 🔧 Configuration

### JWT Authentication

Configure JWT settings in `appsettings.json`:

```json
"JwtSettings": {
  "Secret": "YourSuperSecretKeyHere",
  "Issuer": "IdentityServer",
  "Audience": "IdentityServerAPI",
  "ExpirationInMinutes": 60
}
```

⚠️ **Important**: Change the `Secret` to a strong, random value in production!

### Logging

Serilog is configured to log to:
- Console
- File (`logs/log-{Date}.txt` with daily rolling)

Adjust log levels in `appsettings.json`:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "System": "Warning"
    }
  }
}
```

### CORS

Two CORS policies are configured:
- **AllowAll**: Used in Development (allows all origins)
- **Production**: Restricted to specific domains

Update the Production policy in `ServiceExtensions.cs`:

```csharp
builder.WithOrigins("https://yourdomain.com")
```

## 🧪 Testing

### Using Swagger UI

1. Run the application
2. Navigate to `https://localhost:7xxx/`
3. Use the Swagger UI to test endpoints

### Using Postman

Import the API collection:
1. Open Postman
2. Import > Link > `https://localhost:7xxx/swagger/v1/swagger.json`

## 📦 NuGet Packages Used

### API Project
- `Microsoft.AspNetCore.Authentication.JwtBearer` - JWT authentication
- `Serilog.AspNetCore` - Structured logging
- `Swashbuckle.AspNetCore` - Swagger/OpenAPI
- `Microsoft.AspNetCore.Mvc.Versioning` - API versioning

### Application Project
- `AutoMapper` - Object-object mapping
- `FluentValidation` - Input validation
- `MediatR` - CQRS pattern support

### Infrastructure Project
- `Microsoft.EntityFrameworkCore.SqlServer` - SQL Server provider
- `Microsoft.EntityFrameworkCore.Tools` - EF Core tooling

## 🏛️ Design Patterns

### Repository Pattern
Abstracts data access logic and provides a clean API for data operations.

```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    // ... other methods
}
```

### Unit of Work Pattern
Manages transactions and ensures data consistency.

```csharp
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
}
```

### Result Pattern
Provides consistent success/failure responses.

```csharp
public class Result<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
}
```

## 🔐 Security Best Practices

1. **JWT Configuration**: Store JWT secrets in environment variables or Azure Key Vault
2. **Connection Strings**: Use User Secrets for development, Azure Key Vault for production
3. **CORS**: Configure specific origins in production
4. **HTTPS**: Always use HTTPS in production
5. **Input Validation**: All inputs are validated using FluentValidation
6. **SQL Injection**: Protected by EF Core parameterized queries

## 📝 Common Commands

### Entity Framework Migrations

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API

# Update database
dotnet ef database update --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API

# Remove last migration
dotnet ef migrations remove --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API

# Generate SQL script
dotnet ef migrations script --project src/IdentityServer.Infrastructure --startup-project src/IdentityServer.API
```

### Build and Run

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run API project
dotnet run --project src/IdentityServer.API

# Run with specific environment
dotnet run --project src/IdentityServer.API --environment Production

# Watch mode (auto-reload on changes)
dotnet watch run --project src/IdentityServer.API
```

## 🚢 Deployment

### Docker Support (Future)

Add a Dockerfile for containerization:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/IdentityServer.API/IdentityServer.API.csproj", "src/IdentityServer.API/"]
RUN dotnet restore "src/IdentityServer.API/IdentityServer.API.csproj"
COPY . .
WORKDIR "/src/src/IdentityServer.API"
RUN dotnet build "IdentityServer.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IdentityServer.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IdentityServer.API.dll"]
```

### Azure Deployment

1. Publish to Azure App Service
2. Configure connection strings in Azure Portal
3. Set up Azure SQL Database
4. Configure Application Insights for monitoring

## 📖 Additional Resources

- [Clean Architecture by Uncle Bob](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [AutoMapper Documentation](https://docs.automapper.org/)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License.

## 👥 Authors

- Hady Ahmed - Initial work

## 🙏 Acknowledgments

- Clean Architecture principles by Robert C. Martin
- ASP.NET Core team
- Open source community
