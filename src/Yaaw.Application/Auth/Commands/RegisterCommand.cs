using MediatR;
using Yaaw.Application.DTOs.Auth;
using Yaaw.Application.Interfaces;
using Yaaw.Domain.Entities;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Application.Auth.Commands;

public sealed record RegisterCommand(string Email, string Name, string Password) : IRequest<RegisterResult>;

public sealed record RegisterResult
{
    public bool Succeeded { get; init; }
    public AuthResponseDto? Response { get; init; }
    public string? ErrorDetail { get; init; }
    public IDictionary<string, string[]>? ValidationErrors { get; init; }
}

internal sealed class RegisterHandler(
    IIdentityService identityService,
    ITokenService tokenService,
    IUserRepository userRepository)
    : IRequestHandler<RegisterCommand, RegisterResult>
{
    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken ct)
    {
        var (succeeded, identityUserId, errors) = await identityService.CreateUserAsync(request.Email, request.Password);

        if (!succeeded)
        {
            if (errors is not null)
            {
                return new RegisterResult { Succeeded = false, ValidationErrors = errors };
            }

            return new RegisterResult { Succeeded = false, ErrorDetail = "Failed to create user." };
        }

        await identityService.AddToRoleAsync(identityUserId!, "User");

        var user = new User
        {
            Id = User.NewId(),
            Email = request.Email,
            Name = request.Name,
            CreatedAtUtc = DateTime.UtcNow,
            IdentityId = identityUserId!,
        };

        await userRepository.AddAsync(user, ct);
        await userRepository.SaveChangesAsync(ct);

        string token = await tokenService.GenerateTokenAsync(identityUserId!, user.Id, user.Email, user.Name);

        return new RegisterResult
        {
            Succeeded = true,
            Response = new AuthResponseDto(token, user.Id, user.Email, user.Name),
        };
    }
}
