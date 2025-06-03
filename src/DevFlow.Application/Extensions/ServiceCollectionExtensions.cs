using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DevFlow.Application.Extensions;

/// <summary>
/// Extension methods for configuring application services.
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Adds application services to the service collection.
  /// </summary>
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    var assembly = Assembly.GetExecutingAssembly();

    // Add MediatR
    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

    // Add FluentValidation
    services.AddValidatorsFromAssembly(assembly);

    return services;
  }
}