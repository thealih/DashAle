using DNTPersianUtils.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PriceTracker.Application.Models.Statics;

namespace PriceTracker.Application.Services.Utils
{
    public static class ValidateUtil
    {
        public static bool IsValidEmail(this string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            Regex regex = new Regex(AppRegex.Email);
            return regex.IsMatch(email);
        }

        public static bool IsValidMobile(this string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
            {
                return false;
            }
            return mobile.IsValidIranianMobileNumber();
        }

        public static string NormalizeMobile(this string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
            {
                return null;
            }

            if (mobile.IsValidIranianMobileNumber())
            {
                return $"0{mobile.Substring(mobile.Length - 10, 10)}";
            }
            return null;
        }

        public static DateTime? ToDateTime(this string birthDate)
        {
            if (string.IsNullOrWhiteSpace(birthDate))
            {
                return null;
            }

            if (birthDate.IsValidPersianDateTime())
            {
                return birthDate.ToGregorianDateTime();
            }
            return null;
        }
    }
}
