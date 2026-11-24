using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.Entities.Common;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Domain.Paging;

namespace PriceTracker.Domain.IRepositories.Common
{
    public interface IRepository<EntityBase> where EntityBase : class
    {
        /// <summary>
        /// دریافت یک موجود بر اساس آی دی موجود
        /// Traking = true
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<EntityBase?> GetByIdAsync(Guid id);

        /// <summary>
        /// دریافت یک موجود بر اساس شرط ارسالی
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="traking"></param>
        /// <param name="includes"></param>
        /// <returns></returns>
        Task<EntityBase> GetAsync(Expression<Func<EntityBase, bool>> predicate,
            bool traking = false,
            params Expression<Func<EntityBase, object>>[] includes);

        /// <summary>
        /// دریافت همه موجودیت ها با قابلیت کوئری
        /// </summary>
        /// <param name="tracking"></param>
        /// <returns></returns>
        IQueryable<EntityBase> GetAllQueryableAsync(bool tracking = false);

        /// <summary>
        /// چک کردن وجود موجودیتی با شرایط اعمالی
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        Task<bool> AnyAsync(Expression<Func<EntityBase, bool>> predicate = null);

        /// <summary>
        /// دریافت تعداد موجودیت ها با شرایط ارسالی
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        Task<int> GetCountAsync(Expression<Func<EntityBase, bool>>? predicate = null);

        /// <summary>
        /// اضافه کردن موجودیت جدید
        /// </summary>
        /// <param name="entity"></param>
        void Insert(EntityBase entity);

        /// <summary>
        /// اضافه کردن موجودیت های جدید
        /// </summary>
        /// <param name="entities"></param>
        void Insert(List<EntityBase> entities);

        /// <summary>
        /// اضافه کردن موجودیت های جدید
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>شناسه موجودیت</returns>
        Task<Guid> InsertAsync(EntityBase entity);

        /// <summary>
        /// بروزرسانی موجودیت
        /// </summary>
        /// <param name="entity"></param>
        void Update(EntityBase entity);

        /// <summary>
        /// حذف منطقی یک موجود
        /// </summary>
        /// <param name="entity"></param>
        void SoftDelete(EntityBase entity);

        /// <summary>
        /// حذف موجود از پایگاه داده
        /// </summary>
        /// <param name="entity"></param>
        void Delete(EntityBase entity);

        /// <summary>
        /// اعمال تغییرات روی پایگاه داده
        /// </summary>
        /// <returns></returns>
        Task SaveChangesAsync();
    }
}
