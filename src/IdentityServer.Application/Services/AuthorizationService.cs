using IdentityServer.Application.Interfaces;
using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Interfaces;
using IdentityServer.Shared.Common;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace IdentityServer.Application.Services;

public class AuthorizationService : IAuthorizationService
{
    private readonly IRepository<AuthorizationCode> _authCodeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(
        IRepository<AuthorizationCode> authCodeRepository,
        IUnitOfWork unitOfWork,
        ILogger<AuthorizationService> logger)
    {
        _authCodeRepository = authCodeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<AuthorizationCode>> GenerateAuthorizationCodeAsync(
        long userId, 
        long clientId, 
        string[] scopes, 
        string redirectUri,
        CancellationToken cancellationToken)
    {
        try
        {
            // Generate a secure authorization code
            var code = GenerateSecureCode();

            // Authorization codes typically expire in 10 minutes
            var expiresAt = DateTime.UtcNow.AddMinutes(10);

            var authorizationCode = new AuthorizationCode
            {
                Code = code,
                UserId = userId,
                ClientId = clientId,
                RedirectUri = redirectUri,
                Scopes = string.Join(" ", scopes),
                ExpiresAt = expiresAt,
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            await _authCodeRepository.AddAsync(authorizationCode, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Authorization code generated for user {UserId} and client {ClientIde}", 
                userId, 
                clientId);

            return Result<AuthorizationCode>.Success(authorizationCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating authorization code for user {UserId}", userId);
            return Result<AuthorizationCode>.Failure($"Failed to generate authorization code: {ex.Message}");
        }
    }

    private static string GenerateSecureCode()
    {
        // Generate a cryptographically secure random code
        // Using 32 bytes (256 bits) for security, encoded as base64url
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        // Convert to base64url format (RFC 4648)
        var base64 = Convert.ToBase64String(bytes);
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}