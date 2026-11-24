using MediatR;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Commands.Catalog.AddLink;

public sealed record ProductAddLinkCommand(string? BrandId, string? Url) : IRequest<ProductView>;
