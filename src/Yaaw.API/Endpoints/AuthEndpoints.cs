using MediatR;
using Yaaw.Application.Auth.Commands;
using Yaaw.Application.Auth.Queries;
using Yaaw.Application.DTOs.Auth;
using Yaaw.Application.Interfaces;

namespace Yaaw.API.Endpoints;

internal static class AuthEndpoints
{
    public static WebApplication MapAuthApi(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/auth");

        group.MapPost("/register", Register)
            .WithName(nameof(Register))
            .WithSummary("Register a new user")
            .WithDescription("Creates a new user account and returns a JWT token.")
            .Produces<AuthResponseDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", Login)
            .WithName(nameof(Login))
            .WithSummary("Login")
            .WithDescription("Authenticates a user and returns a JWT token.")
            .Produces<AuthResponseDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/profile", GetProfile)
            .WithName(nameof(GetProfile))
            .WithSummary("Get user profile")
            .WithDescription("Returns the authenticated user's profile.")
            .Produces<UserProfileDto>()
            .RequireAuthorization();

        group.MapPut("/profile", UpdateProfile)
            .WithName(nameof(UpdateProfile))
            .WithSummary("Update user profile")
            .WithDescription("Updates the authenticated user's display name.")
            .Produces<UserProfileDto>()
            .ProducesValidationProblem()
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Register(
        RegisterDto dto,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RegisterCommand(dto.Email, dto.Name, dto.Password), cancellationToken);

        if (!result.Succeeded)
        {
            if (result.ValidationErrors is not null)
            {
                return Results.ValidationProblem(result.ValidationErrors);
            }

            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: result.ErrorDetail ?? "Registration failed.");
        }

        return Results.Created("/api/auth/profile", result.Response);
    }

    private static async Task<IResult> Login(
        LoginDto dto,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new LoginCommand(dto.Email, dto.Password), cancellationToken);

        if (!result.Succeeded)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: result.ErrorDetail ?? "Authentication failed.");
        }

        return Results.Ok(result.Response);
    }

    private static async Task<IResult> GetProfile(
        ICurrentUserService currentUser,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.GetUserId();

        var profile = await mediator.Send(new GetProfileQuery(userId), cancellationToken);

        return profile is null
            ? Results.NotFound()
            : Results.Ok(profile);
    }

    private static async Task<IResult> UpdateProfile(
        UpdateProfileDto dto,
        ICurrentUserService currentUser,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.GetUserId();

        var profile = await mediator.Send(new UpdateProfileCommand(userId, dto.Name), cancellationToken);

        return profile is null
            ? Results.NotFound()
            : Results.Ok(profile);
    }
}
