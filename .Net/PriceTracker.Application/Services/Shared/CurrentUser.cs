using Microsoft.AspNetCore.Http;
using PriceTracker.Application.Models.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Services.Shared
{
    public interface ICurrentUser
    {
        string GetClient { get; }
        bool IsAuthenticated { get; }
    }
    public class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetClient =>
            GetClaim(ClaimStatics.Client);

        public string GetUserName =>
            GetClaim(ClaimStatics.UserName);

        private string GetClaim(string type)
        {
            var claim = _httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(x => x.Type == type);
            return claim.Value;
        }

        public bool IsAuthenticated =>
            _httpContextAccessor
                .HttpContext?
                .User
                .Identity?
                .IsAuthenticated ??
            throw new ApplicationException("User context is unavailable");
    }
}
