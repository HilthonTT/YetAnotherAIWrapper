using MediatR;
using Yaaw.Application.DTOs.Auth;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Application.Auth.Queries;

public sealed record GetProfileQuery(string UserId) : IRequest<UserProfileDto?>;

internal sealed class GetProfileHandler(IUserRepository userRepository)
    : IRequestHandler<GetProfileQuery, UserProfileDto?>
{
    public async Task<UserProfileDto?> Handle(GetProfileQuery request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct);

        if (user is null)
        {
            return null;
        }

        return new UserProfileDto(user.Id, user.Email, user.Name, user.CreatedAtUtc);
    }
}
