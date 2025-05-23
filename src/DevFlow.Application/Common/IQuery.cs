using DevFlow.SharedKernel.Common;
using MediatR;

namespace DevFlow.Application.Common;

/// <summary>
/// Marker interface for queries.
/// </summary>
/// <typeparam name="TResponse">The response type</typeparam>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}

/// <summary>
/// Base interface for query handlers.
/// </summary>
/// <typeparam name="TQuery">The query type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}