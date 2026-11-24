using MediatR;

namespace PriceTracker.Application.Features.Queries.Dashboard.Summary;

public sealed record DashboardSummaryQuery : IRequest<object>;
