using MediatR;
using Yaaw.Application.DTOs.Auth;
using Yaaw.Application.Interfaces;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Application.Auth.Commands;

public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public sealed record LoginResult
{
    public bool Succeeded { get; init; }
    public AuthResponseDto? Response { get; init; }
    public string? ErrorDetail { get; init; }
}

internal sealed class LoginHandler(
    IIdentityService identityService,
    ITokenService tokenService,
    IUserRepository userRepository)
    : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken ct)
    {
        var (succeeded, identityUserId) = await identityService.ValidateCredentialsAsync(request.Email, request.Password);

        if (!succeeded || identityUserId is null)
        {
            return new LoginResult { Succeeded = false, ErrorDetail = "Invalid email or password." };
        }

        var user = await userRepository.GetByIdentityIdAsync(identityUserId, ct);

        if (user is null)
        {
            return new LoginResult { Succeeded = false, ErrorDetail = "User profile not found." };
        }

        string token = await tokenService.GenerateTokenAsync(identityUserId, user.Id, user.Email, user.Name);

        return new LoginResult
        {
            Succeeded = true,
            Response = new AuthResponseDto(token, user.Id, user.Email, user.Name),
        };
    }
}
