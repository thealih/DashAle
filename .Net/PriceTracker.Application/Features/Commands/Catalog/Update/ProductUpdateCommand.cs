using MediatR;
using System.ComponentModel.DataAnnotations;

namespace PriceTracker.Application.Features.Commands.Catalog.Update;

public enum ProductAction
{
    [Display(Name = "فعال‌سازی پایش")]
    Track,

    [Display(Name = "توقف پایش")]
    Untrack,

    [Display(Name = "حذف")]
    Delete,

    [Display(Name = "بازیابی")]
    Restore
}

public sealed record ProductUpdateCommand(long[] Ids, ProductAction Action, string? Actor) : IRequest<int>;
