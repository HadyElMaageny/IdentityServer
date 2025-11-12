using IdentityServer.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Infrastructure.Data;

/// <summary>
/// Seeds initial data for testing and development
/// </summary>
public static class DataSeeder
{
    public static async Task SeedDataAsync(
        ApplicationDbContext context,
        IPasswordHasher<User> passwordHasher,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("Starting data seeding...");

            // Seed Scopes
            await SeedScopesAsync(context, logger);

            // Seed Users
            await SeedUsersAsync(context, passwordHasher, logger);

            // Seed Clients
            await SeedClientsAsync(context, passwordHasher, logger);

            // Seed ClientScope relationships
            await SeedClientScopesAsync(context, logger);

            logger.LogInformation("Data seeding completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding data");
            throw;
        }
    }

    private static async Task SeedScopesAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.Scopes.AnyAsync())
        {
            logger.LogInformation("Scopes already exist, skipping scope seeding");
            return;
        }

        logger.LogInformation("Seeding scopes...");

        var scopes = new List<Scope>
        {
            new()
            {
                Name = "openid",
                DisplayName = "OpenID",
                Description = "OpenID Connect scope for authentication",
                Required = true,
                Emphasize = false
            },
            new()
            {
                Name = "profile",
                DisplayName = "User Profile",
                Description = "Access to user profile information (name, username, etc.)",
                Required = false,
                Emphasize = false
            },
            new()
            {
                Name = "email",
                DisplayName = "Email Address",
                Description = "Access to user email address",
                Required = false,
                Emphasize = false
            },
            new()
            {
                Name = "address",
                DisplayName = "Physical Address",
                Description = "Access to user physical address",
                Required = false,
                Emphasize = false
            },
            new()
            {
                Name = "phone",
                DisplayName = "Phone Number",
                Description = "Access to user phone number",
                Required = false,
                Emphasize = false
            },
            new()
            {
                Name = "offline_access",
                DisplayName = "Offline Access",
                Description = "Access to refresh tokens for offline access",
                Required = false,
                Emphasize = true
            }
        };

        await context.Scopes.AddRangeAsync(scopes);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} scopes", scopes.Count);
    }

    private static async Task SeedUsersAsync(
        ApplicationDbContext context,
        IPasswordHasher<User> passwordHasher,
        ILogger logger)
    {
        if (await context.Users.AnyAsync())
        {
            logger.LogInformation("Users already exist, skipping user seeding");
            return;
        }

        logger.LogInformation("Seeding users...");

        var users = new List<User>
        {
            new()
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "", // Will be hashed below
                IsActive = true
            },
            new()
            {
                Username = "admin",
                Email = "admin@example.com",
                Password = "", // Will be hashed below
                IsActive = true
            },
            new()
            {
                Username = "alice",
                Email = "alice@example.com",
                Password = "", // Will be hashed below
                IsActive = true
            },
            new()
            {
                Username = "bob",
                Email = "bob@example.com",
                Password = "", // Will be hashed below
                IsActive = true
            }
        };

        // Hash passwords (all users have password: "Password123!")
        foreach (var user in users)
        {
            user.Password = passwordHasher.HashPassword(user, "Password123!");
        }

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} users (password: Password123!)", users.Count);
    }

    private static async Task SeedClientsAsync(
        ApplicationDbContext context,
        IPasswordHasher<User> passwordHasher,
        ILogger logger)
    {
        if (await context.Clients.AnyAsync())
        {
            logger.LogInformation("Clients already exist, skipping client seeding");
            return;
        }

        logger.LogInformation("Seeding clients...");

        // Create a dummy user for password hashing
        var dummyUser = new User();

        var clients = new List<Client>
        {
            new()
            {
                ClientIdentifier = "webapp",
                ClientSecret = passwordHasher.HashPassword(dummyUser, "webapp_secret"),
                ClientName = "Test Web Application",
                RedirectUris = "https://localhost:5001/callback,https://localhost:5001/signin-oidc,http://localhost:3000/callback",
                PostLogoutRedirectUris = "https://localhost:5001/signout-callback-oidc,http://localhost:3000/",
                AllowedGrantTypes = "authorization_code,refresh_token",
                AllowOfflineAccess = true,
                Enabled = true,
                RequireClientSecret = true
            },
            new()
            {
                ClientIdentifier = "spa",
                ClientSecret = passwordHasher.HashPassword(dummyUser, "spa_secret"),
                ClientName = "Single Page Application",
                RedirectUris = "http://localhost:3000/callback,http://localhost:4200/callback",
                PostLogoutRedirectUris = "http://localhost:3000/,http://localhost:4200/",
                AllowedGrantTypes = "authorization_code",
                AllowOfflineAccess = false,
                Enabled = true,
                RequireClientSecret = false // SPA typically don't use client secrets
            },
            new()
            {
                ClientIdentifier = "mobile-app",
                ClientSecret = passwordHasher.HashPassword(dummyUser, "mobile_secret"),
                ClientName = "Mobile Application",
                RedirectUris = "myapp://callback",
                PostLogoutRedirectUris = "myapp://",
                AllowedGrantTypes = "authorization_code,refresh_token",
                AllowOfflineAccess = true,
                Enabled = true,
                RequireClientSecret = false
            },
            new()
            {
                ClientIdentifier = "postman",
                ClientSecret = passwordHasher.HashPassword(dummyUser, "postman_secret"),
                ClientName = "Postman Testing Client",
                RedirectUris = "https://oauth.pstmn.io/v1/callback,https://www.getpostman.com/oauth2/callback",
                PostLogoutRedirectUris = "https://www.getpostman.com/",
                AllowedGrantTypes = "authorization_code,refresh_token",
                AllowOfflineAccess = true,
                Enabled = true,
                RequireClientSecret = true
            }
        };

        await context.Clients.AddRangeAsync(clients);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} clients", clients.Count);
        logger.LogInformation("Client credentials:");
        logger.LogInformation("  webapp: client_secret = webapp_secret");
        logger.LogInformation("  spa: client_secret = spa_secret");
        logger.LogInformation("  mobile-app: client_secret = mobile_secret");
        logger.LogInformation("  postman: client_secret = postman_secret");
    }

    private static async Task SeedClientScopesAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.ClientScopes.AnyAsync())
        {
            logger.LogInformation("ClientScopes already exist, skipping ClientScope seeding");
            return;
        }

        logger.LogInformation("Seeding client-scope relationships...");

        // Get all clients and scopes
        var clients = await context.Clients.ToListAsync();
        var scopes = await context.Scopes.ToListAsync();

        var clientScopes = new List<ClientScope>();

        // Map scopes by name for easy lookup
        var scopeMap = scopes.ToDictionary(s => s.Name, s => s.Id);

        foreach (var client in clients)
        {
            // All clients get openid, profile, and email
            var defaultScopes = new[] { "openid", "profile", "email" };

            foreach (var scopeName in defaultScopes)
            {
                if (scopeMap.TryGetValue(scopeName, out var scopeId))
                {
                    clientScopes.Add(new ClientScope
                    {
                        ClientId = client.Id,
                        ScopeId = scopeId
                    });
                }
            }

            // Clients that allow offline access also get the offline_access scope
            if (client.AllowOfflineAccess && scopeMap.TryGetValue("offline_access", out var offlineScopeId))
            {
                clientScopes.Add(new ClientScope
                {
                    ClientId = client.Id,
                    ScopeId = offlineScopeId
                });
            }

            // Add address and phone scopes to webapp and postman clients
            if (client.ClientIdentifier is "webapp" or "postman")
            {
                if (scopeMap.TryGetValue("address", out var addressScopeId))
                {
                    clientScopes.Add(new ClientScope
                    {
                        ClientId = client.Id,
                        ScopeId = addressScopeId
                    });
                }

                if (scopeMap.TryGetValue("phone", out var phoneScopeId))
                {
                    clientScopes.Add(new ClientScope
                    {
                        ClientId = client.Id,
                        ScopeId = phoneScopeId
                    });
                }
            }
        }

        await context.ClientScopes.AddRangeAsync(clientScopes);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} client-scope relationships", clientScopes.Count);
    }
}
