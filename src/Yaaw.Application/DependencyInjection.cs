using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Yaaw.Application.Behaviors;
using Yaaw.Application.DTOs.Conversations;
using Yaaw.Application.Services;
using Yaaw.Application.Sorting;
using Yaaw.Domain.Entities;

namespace Yaaw.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddTransient<DataShapingService>();
        services.AddTransient<SortMappingProvider>();

        services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<Conversation, ConversationDto>>(
            _ => ConversationMappings.SortMappings);

        return services;
    }
}
