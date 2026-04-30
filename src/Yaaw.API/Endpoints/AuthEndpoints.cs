using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Yaaw.API.Database;
using Yaaw.API.DTOs.Auth;
using Yaaw.API.Entities;
using Yaaw.API.Services.Auth;

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
        UserManager<IdentityUser> userManager,
        AppDbContext dbContext,
        TokenService tokenService,
        IValidator<RegisterDto> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(dto, cancellationToken);

        IdentityUser? existingIdentity = await userManager.FindByEmailAsync(dto.Email);
        if (existingIdentity is not null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: "A user with this email already exists.");
        }

        var identityUser = new IdentityUser
        {
            UserName = dto.Email,
            Email = dto.Email,
        };

        IdentityResult result = await userManager.CreateAsync(identityUser, dto.Password);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(
                result.Errors.ToDictionary(
                    e => e.Code,
                    e => new[] { e.Description }));
        }

        await userManager.AddToRoleAsync(identityUser, "User");

        var user = new User
        {
            Id = User.NewId(),
            Email = dto.Email,
            Name = dto.Name,
            CreatedAtUtc = DateTime.UtcNow,
            IdentityId = identityUser.Id,
        };

        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        string token = await tokenService.GenerateTokenAsync(identityUser, user);

        return Results.Created(
            $"/api/auth/profile",
            new AuthResponseDto(token, user.Id, user.Email, user.Name));
    }

    private static async Task<IResult> Login(
        LoginDto dto,
        UserManager<IdentityUser> userManager,
        AppDbContext dbContext,
        TokenService tokenService,
        IValidator<LoginDto> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(dto, cancellationToken);

        IdentityUser? identityUser = await userManager.FindByEmailAsync(dto.Email);
        if (identityUser is null || !await userManager.CheckPasswordAsync(identityUser, dto.Password))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "Invalid email or password.");
        }

        User? user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.IdentityId == identityUser.Id, cancellationToken);

        if (user is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "User profile not found.");
        }

        string token = await tokenService.GenerateTokenAsync(identityUser, user);

        return Results.Ok(new AuthResponseDto(token, user.Id, user.Email, user.Name));
    }

    private static async Task<IResult> GetProfile(
        CurrentUserService currentUser,
        UserManager<IdentityUser> userManager,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.GetUserId();

        User? dbUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.IdentityId == userId, cancellationToken);

        if (dbUser is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new UserProfileDto(dbUser.Id, dbUser.Email, dbUser.Name, dbUser.CreatedAtUtc));
    }

    private static async Task<IResult> UpdateProfile(
        UpdateProfileDto dto,
        CurrentUserService currentUser,
        AppDbContext dbContext,
        IValidator<UpdateProfileDto> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(dto, cancellationToken);

        string userId = currentUser.GetUserId();

        User? user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Results.NotFound();
        }

        user.Name = dto.Name;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new UserProfileDto(user.Id, user.Email, user.Name, user.CreatedAtUtc));
    }
}
