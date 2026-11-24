using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Infrastracture.Models
{
    public static class ExternalErrorMessages
    {
        public const string ErrorToken = "سرویس دریافت توکن دارای خطا می باشد";
        public const string ErrorSearchUser = "سرویس دریافت اطلاعات کاربران دارای خطا می باشد";
        public const string ErrorAddClient = "سرویس افزودن کلاینت دارای خطا می باشد";
        public const string ErrorApiService = "سرویس Api دارای خطا می باشد";
        public const string ErrorSmsService = "سرویس ارسال اس ام اس دارای خطا می باشد";
        public const string ErrorEmailService = "سرویس ارسال ایمیل دارای خطا می باشد";
    }
}
