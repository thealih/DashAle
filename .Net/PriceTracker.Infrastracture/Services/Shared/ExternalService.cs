using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Infrastracture.Extensions;
using PriceTracker.Domain.IServices;
using PriceTracker.Domain.Models.DTOs.ExDTOs;
using PriceTracker.Domain.Models.Enums;

namespace PriceTracker.Infrastracture.Services.Shared
{
    public class ExternalService : IExternalService
    {
        private readonly HttpClient _httpClient;
        private Dictionary<string, string> _mappings;

        public ExternalService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<object> CallServiceWithMappingAsync<TResponse>(
            string baseAddress,
            string endpoint,
            Dictionary<string, object> requestData,
            HttpMethod method,
            Dictionary<string, string> mappings = null,
            Dictionary<string, string> headers = null,
            string nestedPropertyPath = null,
            AuthConfigDTO authConfig = null)
            where TResponse : class, new()
        {
            try
            {
                _httpClient.BaseAddress = new Uri(baseAddress);

                // ارسال درخواست به سرویس خارجی
                var content = await CallExternalServiceAsync(endpoint, requestData, method, headers, nestedPropertyPath, authConfig);

                if (mappings is null)
                {
                    return JsonConvert.DeserializeObject<TResponse>(content);
                }

                _mappings = mappings;

                // تبدیل Dictionary به مدل مورد نظر
                return MapToDto<TResponse>(content);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in service call with mapping: {ex.Message}", ex);
            }
        }

        //private async Task<Dictionary<string, object>> CallExternalServiceAsync(
        private async Task<string> CallExternalServiceAsync(
            string endpoint,
            Dictionary<string, object> requestData,
            HttpMethod method,
            Dictionary<string, string> headers,
            string nestedPropertyPath,
            AuthConfigDTO authConfig)
        {

            var httpRequest = new HttpRequestMessage(method, endpoint)
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(requestData),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };

            // افزودن هدرهای احراز هویت
            await AddAuthenticationHeaders(httpRequest, authConfig);

            AddHeaders(httpRequest, headers);

            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();

            string content = responseContent;
            //if (!string.IsNullOrWhiteSpace(nestedPropertyPath))
            //    content = GetNestedModelValue(responseContent, nestedPropertyPath);

            //var responseDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);



            // return responseDict;
            return content;
        }


        private void AddHeaders(HttpRequestMessage request, Dictionary<string, string> headers)
        {
            if (headers == null) return;

            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }

        private async Task AddAuthenticationHeaders(HttpRequestMessage request, AuthConfigDTO authConfig)
        {
            switch (authConfig.AuthType)
            {
                case AuthTypeEnum.BearerToken:
                    //var token = await _tokenProvider.GetBearerToken();
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authConfig.Token);
                    break;

                case AuthTypeEnum.BasicAuth:
                    var basicAuthValue = Convert.ToBase64String(
                        System.Text.Encoding.ASCII.GetBytes($"{authConfig.Username}:{authConfig.Password}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthValue);
                    break;

                case AuthTypeEnum.ApiKey:
                    request.Headers.Add(authConfig.ApiKeyHeader, authConfig.ApiKey);
                    break;

                case AuthTypeEnum.OAuth2:
                    //var oauthToken = await _tokenProvider.GetOAuthToken();
                    var oauthToken = authConfig.Token;
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oauthToken);
                    break;

                //case AuthTypeEnum.Custom:
                //    foreach (var header in authConfig.CustomHeaders)
                //    {
                //        request.Headers.Add(header.Key, header.Value);
                //    }
                //    break;

                case AuthTypeEnum.None:
                default:
                    break;
            }
        }

        public object MapToDto<T>(string json) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentNullException(nameof(json), "JSON ورودی نمی‌تواند خالی باشد.");

            try
            {
                // اطمینان از رمزگذاری UTF-8
                json = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(json.Trim()));

                // بررسی معتبر بودن JSON
                if (!IsValidJson(json))
                    throw new InvalidOperationException("JSON ورودی نامعتبر است.");

                // پارس JSON به JToken
                JToken jToken = JToken.Parse(json);
                List<T> resultList = new List<T>();

                // اگر JSON یک آرایه باشد
                if (jToken.Type == JTokenType.Array)
                {
                    foreach (JObject jObject in jToken)
                    {
                        T item = new T();
                        MapProperties(jObject, item, "");
                        resultList.Add(item);
                    }
                    return resultList;
                }
                // اگر JSON یک شیء باشد
                else if (jToken.Type == JTokenType.Object)
                {
                    T item = new T();
                    MapProperties((JObject)jToken, item, "");
                    return item;
                }
                else
                {
                    throw new InvalidOperationException("JSON ورودی باید یک شیء یا آرایه باشد.");
                }
            }
            catch (JsonReaderException ex)
            {
                throw new InvalidOperationException($"خطا در پارس JSON: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطا در تبدیل JSON به DTO: {ex.Message}", ex);
            }
        }

        private static bool IsValidJson(string json)
        {
            try
            {
                JToken.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private object ConvertValue(JToken jToken, Type targetType, string dtoPath)
        {
            try
            {
                Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                // بررسی مقدار null برای پراپرتی‌های nullable
                if (jToken.Type == JTokenType.Null && Nullable.GetUnderlyingType(targetType) != null)
                {
                    Console.WriteLine($"مقدار JSON برای '{dtoPath}' null است و پراپرتی nullable است.");
                    return null;
                }

                // تبدیل به string
                if (TryConvertToString(jToken, underlyingType, dtoPath, out object stringResult))
                    return stringResult;

                // تبدیل به int
                if (TryConvertToInt(jToken, underlyingType, targetType, dtoPath, out object intResult))
                    return intResult;

                // تبدیل به DateTime
                if (TryConvertToDateTime(jToken, underlyingType, targetType, dtoPath, out object dateTimeResult))
                    return dateTimeResult;

                // تبدیل به bool
                if (TryConvertToBool(jToken, underlyingType, targetType, dtoPath, out object boolResult))
                    return boolResult;

                // تبدیل به enum
                if (TryConvertToEnum(jToken, underlyingType, targetType, dtoPath, out object enumResult))
                    return enumResult;

                // تبدیل عمومی با JsonConvert
                return ConvertToGenericType(jToken, underlyingType, dtoPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در تبدیل مقدار برای '{dtoPath}': {ex.Message}");
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }

        private bool TryConvertToString(JToken jToken, Type underlyingType, string dtoPath, out object result)
        {
            result = null;
            if (underlyingType != typeof(string))
                return false;

            string value = jToken.ToString();
            Console.WriteLine($"تبدیل به string برای '{dtoPath}' با مقدار '{value}'.");
            result = value;
            return true;
        }

        private bool TryConvertToInt(JToken jToken, Type underlyingType, Type targetType, string dtoPath, out object result)
        {
            result = null;
            if (underlyingType != typeof(int))
                return false;

            if (jToken.Type == JTokenType.Integer)
            {
                int value = jToken.ToObject<int>();
                Console.WriteLine($"تبدیل به int برای '{dtoPath}' با مقدار '{value}'.");
                result = value;
                return true;
            }
            if (jToken.Type == JTokenType.String)
            {
                string stringValue = jToken.ToString().Trim();
                if (int.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out int intValue))
                {
                    Console.WriteLine($"تبدیل رشته به int برای '{dtoPath}' با مقدار '{intValue}'.");
                    result = intValue;
                    return true;
                }
                Console.WriteLine($"نمی‌توان رشته '{stringValue}' را به int تبدیل کرد برای '{dtoPath}'.");
                if (Nullable.GetUnderlyingType(targetType) != null)
                {
                    Console.WriteLine($"پرتاب مقدار null برای '{dtoPath}' چون پراپرتی nullable است.");
                    result = null;
                    return true;
                }
                throw new InvalidOperationException($"نمی‌توان '{stringValue}' را به int تبدیل کرد.");
            }
            if (Nullable.GetUnderlyingType(targetType) != null)
            {
                Console.WriteLine($"پرتاب مقدار null برای '{dtoPath}' چون پراپرتی nullable است.");
                result = null;
                return true;
            }
            throw new InvalidOperationException($"نمی‌توان '{jToken}' را به int تبدیل کرد.");
        }

        private bool TryConvertToDateTime(JToken jToken, Type underlyingType, Type targetType, string dtoPath, out object result)
        {
            result = null;
            if (underlyingType != typeof(DateTime))
                return false;

            if (jToken.Type == JTokenType.Date)
            {
                DateTime value = jToken.ToObject<DateTime>();
                Console.WriteLine($"تبدیل به DateTime برای '{dtoPath}' با مقدار '{value:yyyy-MM-dd}'.");
                result = value;
                return true;
            }
            if (jToken.Type == JTokenType.String)
            {
                string[] formats = {
                "yyyy-MM-dd",
                "yyyy/MM/dd",
                "yyyy-MM-ddTHH:mm:ss",
                "MM/dd/yyyy",
                "dd-MM-yyyy"
            };
                string stringValue = jToken.ToString().Trim();
                if (DateTime.TryParseExact(stringValue, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateValue))
                {
                    Console.WriteLine($"تبدیل رشته به DateTime با TryParseExact برای '{dtoPath}' با مقدار '{dateValue:yyyy-MM-dd}'.");
                    result = dateValue;
                    return true;
                }
                if (DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateValue))
                {
                    Console.WriteLine($"تبدیل رشته به DateTime با TryParse برای '{dtoPath}' با مقدار '{dateValue:yyyy-MM-dd}'.");
                    result = dateValue;
                    return true;
                }
                Console.WriteLine($"نمی‌توان رشته '{stringValue}' را به DateTime تبدیل کرد برای '{dtoPath}'.");
                if (Nullable.GetUnderlyingType(targetType) != null)
                {
                    Console.WriteLine($"پرتاب مقدار null برای '{dtoPath}' چون پراپرتی nullable است.");
                    result = null;
                    return true;
                }
                throw new InvalidOperationException($"نمی‌توان '{stringValue}' را به DateTime تبدیل کرد.");
            }
            if (Nullable.GetUnderlyingType(targetType) != null)
            {
                Console.WriteLine($"پرتاب مقدار null برای '{dtoPath}' چون پراپرتی nullable است.");
                result = null;
                return true;
            }
            throw new InvalidOperationException($"نمی‌توان '{jToken}' را به DateTime تبدیل کرد.");
        }

        private bool TryConvertToBool(JToken jToken, Type underlyingType, Type targetType, string dtoPath, out object result)
        {
            result = null;
            if (underlyingType != typeof(bool))
                return false;

            if (_mappings.TryGetValue("BooleanMappings", out var boolMappingsJson))
            {
                try
                {
                    var boolMappings = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, bool>>>(boolMappingsJson);
                    var mapping = boolMappings.FirstOrDefault(x => x.Key.Equals(dtoPath, StringComparison.OrdinalIgnoreCase)).Value;
                    if (mapping != null)
                    {
                        string tokenValue = jToken.ToString().Trim().ToLowerInvariant();
                        var mappingEntry = mapping.FirstOrDefault(x => x.Key.Equals(tokenValue, StringComparison.OrdinalIgnoreCase));
                        if (mappingEntry.Key != null)
                        {
                            Console.WriteLine($"نگاشت سفارشی برای '{dtoPath}' با مقدار '{tokenValue}' به '{mappingEntry.Value}' پیدا شد.");
                            result = mappingEntry.Value;
                            return true;
                        }
                        Console.WriteLine($"مقدار '{tokenValue}' برای '{dtoPath}' در نگاشت سفارشی پیدا نشد.");
                        if (Nullable.GetUnderlyingType(targetType) != null)
                        {
                            Console.WriteLine($"پرتاب مقدار null برای '{dtoPath}' چون پراپرتی nullable است.");
                            result = null;
                            return true;
                        }
                        throw new InvalidOperationException($"مقدار '{tokenValue}' برای '{dtoPath}' در نگاشت سفارشی پیدا نشد.");
                    }
                    Console.WriteLine($"نگاشت سفارشی برای '{dtoPath}' در BooleanMappings پیدا نشد.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"خطا در پارس BooleanMappings برای '{dtoPath}': {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"کلید BooleanMappings در _mappings پیدا نشد.");
            }

            if (jToken.Type == JTokenType.Boolean)
            {
                bool value = jToken.ToObject<bool>();
                Console.WriteLine($"تبدیل به bool برای '{dtoPath}' با مقدار '{value}'.");
                result = value;
                return true;
            }
            if (jToken.Type == JTokenType.String)
            {
                string stringValue = jToken.ToString().Trim();
                if (bool.TryParse(stringValue, out bool boolValue))
                {
                    Console.WriteLine($"تبدیل رشته به bool برای '{dtoPath}' با مقدار '{boolValue}'.");
                    result = boolValue;
                    return true;
                }
                Console.WriteLine($"نمی‌توان رشته '{stringValue}' را به bool تبدیل کرد برای '{dtoPath}'.");
            }
            if (Nullable.GetUnderlyingType(targetType) != null)
            {
                Console.WriteLine($"پرتاب مقدار null برای '{dtoPath}' چون پراپرتی nullable است.");
                result = null;
                return true;
            }
            throw new InvalidOperationException($"نمی‌توان '{jToken}' را به bool تبدیل کرد.");
        }

        private bool TryConvertToEnum(JToken jToken, Type underlyingType, Type targetType, string dtoPath, out object result)
        {
            result = null;
            if (!underlyingType.IsEnum)
                return false;

            if (jToken.Type == JTokenType.String)
            {
                string stringValue = jToken.ToString().Trim();
                if (Enum.TryParse(underlyingType, stringValue, true, out object enumValue))
                {
                    Console.WriteLine($"تبدیل رشته به enum برای '{dtoPath}' با مقدار '{stringValue}' به '{enumValue}'.");
                    result = enumValue;
                    return true;
                }
                Console.WriteLine($"نمی‌توان رشته '{stringValue}' را به enum '{underlyingType.Name}' تبدیل کرد برای '{dtoPath}'.");
            }
            if (jToken.Type == JTokenType.Integer)
            {
                int intValue = jToken.ToObject<int>();
                if (Enum.IsDefined(underlyingType, intValue))
                {
                    object enumValue = Enum.ToObject(underlyingType, intValue);
                    Console.WriteLine($"تبدیل عدد به enum برای '{dtoPath}' با مقدار '{intValue}' به '{enumValue}'.");
                    result = enumValue;
                    return true;
                }
                Console.WriteLine($"مقدار عددی '{intValue}' برای enum '{underlyingType.Name}' معتبر نیست برای '{dtoPath}'.");
            }
            if (Nullable.GetUnderlyingType(targetType) != null)
            {
                Console.WriteLine($"پرتاب مقدار null برای '{dtoPath}' چون پراپرتی nullable است و مقدار معتبر نیست.");
                result = null;
                return true;
            }
            throw new InvalidOperationException($"نمی‌توان '{jToken}' را به enum '{underlyingType.Name}' تبدیل کرد.");
        }

        private object ConvertToGenericType(JToken jToken, Type underlyingType, string dtoPath)
        {
            var result = JsonConvert.DeserializeObject(jToken.ToString(), underlyingType, new JsonSerializerSettings
            {
                Error = (sender, args) =>
                {
                    Console.WriteLine($"خطا در دی‌سریال‌سازی پراپرتی '{dtoPath}': {args.ErrorContext.Error.Message}");
                    args.ErrorContext.Handled = true;
                }
            });
            Console.WriteLine($"تبدیل به نوع عمومی برای '{dtoPath}' با مقدار '{result}'.");
            return result;
        }

        private void MapProperties(JObject jObject, object target, string parentDtoPath)
        {
            var properties = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                string dtoPath = string.IsNullOrEmpty(parentDtoPath) ? property.Name : $"{parentDtoPath}.{property.Name}";

                if (TryMapCombinedProperty(jObject, target, property, dtoPath))
                    continue;

                if (TryMapListProperty(jObject, target, property, dtoPath))
                    continue;

                if (TryMapNestedObjectProperty(jObject, target, property, dtoPath))
                    continue;

                MapSimpleProperty(jObject, target, property, dtoPath);
            }
        }

        private bool TryMapCombinedProperty(JObject jObject, object target, PropertyInfo property, string dtoPath)
        {
            string jsonPath = _mappings.FirstOrDefault(m => m.Key.Equals(dtoPath, StringComparison.OrdinalIgnoreCase)).Value;
            if (string.IsNullOrEmpty(jsonPath) || !jsonPath.Contains(","))
                return false;

            try
            {
                var jsonKeys = jsonPath.Split(',');
                var values = new List<string>();
                foreach (var key in jsonKeys)
                {
                    JToken jToken = jObject.SelectToken(key.Trim(), StringComparison.OrdinalIgnoreCase);
                    if (jToken != null)
                    {
                        values.Add(jToken.ToString());
                    }
                }
                string combinedValue = string.Join(" ", values).Trim();
                if (!string.IsNullOrEmpty(combinedValue))
                {
                    property.SetValue(target, combinedValue);
                    Console.WriteLine($"نگاشت ترکیبی '{jsonPath}' به '{dtoPath}' با مقدار '{combinedValue}' انجام شد.");
                    return true;
                }
                Console.WriteLine($"مقادیر JSON برای '{jsonPath}' در '{dtoPath}' پیدا نشدند.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در نگاشت ترکیبی برای '{dtoPath}': {ex.Message}");
                return true;
            }
        }

        private bool TryMapListProperty(JObject jObject, object target, PropertyInfo property, string dtoPath)
        {
            if (!property.PropertyType.IsGenericType || property.PropertyType.GetGenericTypeDefinition() != typeof(List<>))
                return false;

            var listItemType = property.PropertyType.GetGenericArguments()[0];
            var jsonPathPrefix = _mappings.FirstOrDefault(m => m.Key.Equals(dtoPath, StringComparison.OrdinalIgnoreCase)).Value;
            if (string.IsNullOrEmpty(jsonPathPrefix))
            {
                jsonPathPrefix = dtoPath.Split('.').Last();
                Console.WriteLine($"نگاشت صریح برای لیست '{dtoPath}' یافت نشد، استفاده از نگاشت پیش‌فرض '{jsonPathPrefix}'.");
            }

            JToken jToken = jObject.SelectToken(jsonPathPrefix, StringComparison.OrdinalIgnoreCase);
            if (jToken == null || jToken.Type != JTokenType.Array)
            {
                Console.WriteLine($"آرایه JSON '{jsonPathPrefix}' یافت نشد یا آرایه نیست.");
                return true;
            }

            var list = Activator.CreateInstance(property.PropertyType) as System.Collections.IList;
            foreach (JObject jItem in jToken)
            {
                var listItem = Activator.CreateInstance(listItemType);
                var listItemProperties = listItemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var itemProperty in listItemProperties)
                {
                    string itemDtoPath = $"{dtoPath}.{itemProperty.Name}";
                    string itemJsonPath = _mappings.FirstOrDefault(m => m.Key.Equals(itemDtoPath, StringComparison.OrdinalIgnoreCase)).Value;
                    if (string.IsNullOrEmpty(itemJsonPath))
                    {
                        itemJsonPath = itemProperty.Name;
                        Console.WriteLine($"نگاشت صریح برای پراپرتی لیست '{itemDtoPath}' یافت نشد، استفاده از نگاشت پیش‌فرض '{itemJsonPath}'.");
                    }

                    JToken itemToken = jItem.SelectToken(itemJsonPath, StringComparison.OrdinalIgnoreCase);
                    if (itemToken == null)
                    {
                        Console.WriteLine($"کلید JSON '{itemJsonPath}' در JSON یافت نشد.");
                        continue;
                    }

                    try
                    {
                        var value = ConvertValue(itemToken, itemProperty.PropertyType, itemDtoPath);
                        itemProperty.SetValue(listItem, value);
                        Console.WriteLine($"نگاشت '{itemJsonPath}' به '{itemDtoPath}' با مقدار '{value}' انجام شد.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"خطا در نگاشت مقدار برای '{itemDtoPath}': {ex.Message}");
                    }
                }
                list.Add(listItem);
            }
            property.SetValue(target, list);
            Console.WriteLine($"نگاشت آرایه برای '{dtoPath}' انجام شد.");
            return true;
        }

        private bool TryMapNestedObjectProperty(JObject jObject, object target, PropertyInfo property, string dtoPath)
        {
            if (property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                return false;

            string jsonPathPrefix = _mappings.FirstOrDefault(m => m.Key.Equals(dtoPath, StringComparison.OrdinalIgnoreCase)).Value;
            if (string.IsNullOrEmpty(jsonPathPrefix))
            {
                jsonPathPrefix = dtoPath.Split('.').Last();
                Console.WriteLine($"نگاشت صریح برای شیء تودرتو '{dtoPath}' یافت نشد، استفاده از نگاشت پیش‌فرض '{jsonPathPrefix}'.");
            }

            JToken jToken = jObject.SelectToken(jsonPathPrefix, StringComparison.OrdinalIgnoreCase);
            if (jToken == null || jToken.Type != JTokenType.Object)
            {
                Console.WriteLine($"شیء JSON '{jsonPathPrefix}' یافت نشد یا شیء نیست.");
                return true;
            }

            var nestedObject = property.GetValue(target) ?? Activator.CreateInstance(property.PropertyType);
            MapProperties(jToken as JObject, nestedObject, dtoPath);
            property.SetValue(target, nestedObject);
            Console.WriteLine($"نگاشت شیء تودرتو برای '{dtoPath}' انجام شد.");
            return true;
        }

        private void MapSimpleProperty(JObject jObject, object target, PropertyInfo property, string dtoPath)
        {
            string jsonPathSingle = _mappings.FirstOrDefault(m => m.Key.Equals(dtoPath, StringComparison.OrdinalIgnoreCase)).Value;
            if (string.IsNullOrEmpty(jsonPathSingle))
            {
                jsonPathSingle = dtoPath.Split('.').Last();
                Console.WriteLine($"نگاشت صریح برای '{dtoPath}' یافت نشد، استفاده از نگاشت پیش‌فرض '{jsonPathSingle}'.");
            }

            string adjustedJsonPath = jsonPathSingle;
            string parentDtoPath = dtoPath.Contains(".") ? dtoPath.Substring(0, dtoPath.LastIndexOf('.')) : "";
            if (!string.IsNullOrEmpty(parentDtoPath))
            {
                string parentJsonPath = _mappings.FirstOrDefault(m => m.Key.Equals(parentDtoPath, StringComparison.OrdinalIgnoreCase)).Value;
                if (!string.IsNullOrEmpty(parentJsonPath) && jsonPathSingle.StartsWith(parentJsonPath + ".", StringComparison.OrdinalIgnoreCase))
                {
                    adjustedJsonPath = jsonPathSingle.Substring(parentJsonPath.Length + 1);
                    Console.WriteLine($"مسیر JSON اصلاح شد برای '{dtoPath}': از '{jsonPathSingle}' به '{adjustedJsonPath}'.");
                }
            }

            try
            {
                JToken jToken = jObject.SelectToken(adjustedJsonPath, StringComparison.OrdinalIgnoreCase);
                if (jToken == null)
                {
                    Console.WriteLine($"کلید JSON '{adjustedJsonPath}' در JSON یافت نشد.");
                    return;
                }

                try
                {
                    var value = ConvertValue(jToken, property.PropertyType, dtoPath);
                    property.SetValue(target, value);
                    Console.WriteLine($"نگاشت '{adjustedJsonPath}' به '{dtoPath}' با مقدار '{value}' انجام شد.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"خطا در نگاشت مقدار برای '{dtoPath}': {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در نگاشت کلید JSON '{adjustedJsonPath}' به پراپرتی '{dtoPath}': {ex.Message}");
            }
        }
    }
}
