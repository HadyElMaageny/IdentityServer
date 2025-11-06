using IdentityServer.Application.DTOs;
using IdentityServer.Application.Interfaces;
using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Application.Services;

public class AuthService : IAuthService
{
    private readonly IRepository<User> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        IRepository<User> userRepository,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        IPasswordHasher<User> passwordHasher)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // check username exists
        var existingUser = await _userRepository
            .Query() // we'll add Query() below if your repo doesn't have it
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (existingUser != null)
            throw new Exception("Username already exists.");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            IsActive = true
        };

        user.Password = _passwordHasher.HashPassword(user, request.Password);

        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // issue tokens
        return await _tokenService.GenerateTokensAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository
            .Query()
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null)
            throw new Exception("Invalid credentials.");

        var result = _passwordHasher.VerifyHashedPassword(user, user.Password, request.Password);
        if (result == PasswordVerificationResult.Failed)
            throw new Exception("Invalid credentials.");

        return await _tokenService.GenerateTokensAsync(user);
    }
}