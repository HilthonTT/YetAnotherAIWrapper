using MediatR;
using Yaaw.Application.DTOs.Auth;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Application.Auth.Commands;

public sealed record UpdateProfileCommand(string UserId, string Name) : IRequest<UserProfileDto?>;

internal sealed class UpdateProfileHandler(IUserRepository userRepository)
    : IRequestHandler<UpdateProfileCommand, UserProfileDto?>
{
    public async Task<UserProfileDto?> Handle(UpdateProfileCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct);

        if (user is null)
        {
            return null;
        }

        user.Name = request.Name;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.SaveChangesAsync(ct);

        return new UserProfileDto(user.Id, user.Email, user.Name, user.CreatedAtUtc);
    }
}
