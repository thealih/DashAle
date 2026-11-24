using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.Entities.Common;
using Microsoft.AspNetCore.Http;

namespace PriceTracker.Infrastracture.Interceptors
{
    public class AuditableInterceptor(
    IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            SetDetails(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = new())
        {
            SetDetails(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public void SetDetails(DbContext? dbContext)
        {
            if (dbContext == null) return;

            var username = httpContextAccessor
                .HttpContext?
                .User
                .Claims?
                .FirstOrDefault(c => c.Type.ToLower() == "username")?
                .Value;

            foreach (var entry in dbContext.ChangeTracker.Entries<BaseEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        var now = DateTime.Now;
                        entry.Entity.CreatedAt = now;
                        entry.Entity.ModifiedAt = now;
                        entry.Entity.CreatedBy = username;
                        entry.Entity.IsDeleted = false;
                        break;
                    case EntityState.Modified:
                        entry.Entity.ModifiedAt = DateTime.Now;
                        entry.Entity.ModifiedBy = username;
                        break;
                    case EntityState.Deleted:
                        if (entry.Entity.IsDeleted)
                        {
                            entry.State = EntityState.Modified;
                            entry.Entity.IsDeleted = true;
                            entry.Entity.ModifiedAt = DateTime.Now;
                            entry.Entity.ModifiedBy = username;
                        }
                        break;
                }
            }
        }
    }
}
