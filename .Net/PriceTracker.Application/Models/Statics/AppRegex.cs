using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Models.Statics
{
    public static class AppRegex
    {
        public const string EnglishChar = @"^[a-zA-Z0-9-_]+$";
        public const string Mobile = @"^(?:(?:(?:\\+?|00)(98))|(0))?((?:90|91|92|93|99)[0-9]{8})$";
        public const string PhoneNumber = @"^0[1|3|4|5|6|7|8|9][0-9]{9}$|^02[0-9]{9}$"; // ^0\d{2,3}-\d{8}$
        public const string Email = @"[a-zA-Z0-9.-]+@[a-z-]+\.[a-z]{2,3}";
        public const string Password = @"^(?=^.{8,16}$)((?=.*[A-Za-z0-9])(?=.*[A-Z])(?=.*[a-z]))^.*$";
        public const string Number = @"^[0-9]+$";
    }
}
