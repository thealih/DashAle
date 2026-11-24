using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.Models.Enums
{
    public enum AuthTypeEnum
    {
        None,
        BearerToken,
        BasicAuth,
        ApiKey,
        OAuth2,
        Custom
    }
}
