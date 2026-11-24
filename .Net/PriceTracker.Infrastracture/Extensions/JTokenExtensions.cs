using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Infrastracture.Extensions
{
    public static class JTokenExtensions
    {
        public static JToken SelectToken(this JToken token, string path, StringComparison comparison)
        {
            if (token == null || string.IsNullOrEmpty(path))
            {
                Console.WriteLine($"خطا: token یا path خالی است. path: '{path}'");
                return null;
            }

            // اگر مسیر ساده است (بدون نقطه)
            if (!path.Contains("."))
            {
                if (token is JObject jObject)
                {
                    var property = jObject.Properties()
                        .FirstOrDefault(p => p.Name.Equals(path, comparison));
                    if (property == null)
                    {
                        Console.WriteLine($"کلید '{path}' در JSON پیدا نشد.");
                        return null;
                    }
                    Console.WriteLine($"کلید '{path}' پیدا شد با مقدار '{property.Value}'.");
                    return property.Value;
                }
                Console.WriteLine($"token از نوع JObject نیست برای path: '{path}'.");
                return null;
            }

            // برای مسیرهای نقطه‌دار (مثل pid.id)
            var segments = path.Split('.');
            JToken current = token;
            foreach (var segment in segments)
            {
                if (current is JObject jObject)
                {
                    var property = jObject.Properties()
                        .FirstOrDefault(p => p.Name.Equals(segment, comparison));
                    if (property == null)
                    {
                        Console.WriteLine($"کلید '{segment}' در مسیر '{path}' پیدا نشد.");
                        return null;
                    }
                    Console.WriteLine($"کلید '{segment}' در مسیر '{path}' پیدا شد با مقدار '{property.Value}'.");
                    current = property.Value;
                }
                else
                {
                    Console.WriteLine($"شیء JSON در مسیر '{path}' معتبر نیست برای کلید '{segment}'.");
                    return null;
                }
            }
            return current;
        }
    }
}
