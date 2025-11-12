using IdentityServer.Application.Interfaces;
using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Application.Services;

public class UserConsentService : IUserConsentService
{
    private readonly IRepository<UserConsent> _userConsentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserConsentService> _logger;

    public UserConsentService(IRepository<UserConsent> userConsentRepository, IUnitOfWork unitOfWork,
        ILogger<UserConsentService> logger)
    {
        _userConsentRepository = userConsentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> HasConsentAsync(long userId, long clientId, string scopes,
        CancellationToken cancellationToken)
    {
        var existingConsents = await _userConsentRepository.FindAsync(
            uc => uc.ClientId == clientId && uc.UserId == userId,
            cancellationToken);
        if (!existingConsents.Any())
            return false;
        var existing = existingConsents.First();
        var requestedScopes = scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var grantedScopes = existing.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var hasAllScopes = requestedScopes.All(scope => grantedScopes.Contains(scope));
        return hasAllScopes;
    }

    public async Task GrantConsentAsync(long userId, long clientId, string scopes,
        CancellationToken cancellationToken)
    {
        var consent = new UserConsent
        {
            UserId = userId,
            ClientId = clientId,
            Scopes = scopes,
            GrantedAt = DateTime.UtcNow
        };

        await _userConsentRepository.AddAsync(consent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} granted consent for client {ClientIdentifier} with scopes: {Scopes}",
            userId, clientId, scopes);
    }
}