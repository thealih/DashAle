using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Application.Services.Shared;

namespace PriceTracker.Application.Services.Utils
{
    public static class EnumUtil
    {
        public static string ToEnumTitle(this Enum? obj)
        {
            if (obj == null)
            {
                return "";
            }

            try
            {
                var enumType = obj.GetType();
                var fi = enumType.GetField(obj.ToString()!);
                if (fi == null)
                {
                    return "";
                }

                var attributeDescription = fi.GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault();
                var attributeDisplay = fi.GetCustomAttributes(typeof(DisplayAttribute), true).FirstOrDefault();

                if (attributeDescription != null)
                {
                    return ((DescriptionAttribute)attributeDescription).Description;
                }

                if (attributeDisplay != null)
                {
                    return ((DisplayAttribute)attributeDisplay).Name!;
                }

                return fi.Name;
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static T GetValueFromName<T>(this string name) where T : Enum
        {
            var type = typeof(T);

            foreach (var field in type.GetFields())
            {
                if (Attribute.GetCustomAttribute(field, typeof(DisplayAttribute)) is DisplayAttribute attribute)
                {
                    if (attribute.Name == name)
                    {
                        return (T)field.GetValue(null);
                    }
                }

                if (field.Name == name)
                {
                    return (T)field.GetValue(null);
                }
            }

            throw new ArgumentOutOfRangeException(nameof(name));
        }

        public static List<EnumObject<T>> EnumToJson<T>() where T : struct, Enum
        {
            var type = typeof(T);
            var enums = Enum.GetValues<T>();
            var values = new List<EnumObject<T>>();
            foreach (var item in enums)
            {
                string name = item.ToEnumTitle();
                var value = new EnumObject<T>()
                {
                    Title = name,
                    Value = name.GetValueFromName<T>()
                };

                values.Add(value);
            };

            return values;
        }
    }
}
