namespace PriceTracker.Application.Models.Statics.Errors;

public static class ValidatorErrors
{
    public static string NotNull(string name) => $"مقدار فیلد {name} را ارسال کنید";
    public static string NotEmpty(string name) => $"مقدار فیلد {name} نمی تواند خالی باشد";
    public static string NotEqual(string name, string value) => $"مقدار فیلد {name} نمی تواند برابر با {value} باشد";
    public static string MaxLength(string name, int length) => $"مقدار فیلد {name} نمی تواند بیشتر از {length} کاراکتر باشد";
    public static string MinLength(string name, int length) => $"مقدار فیلد {name} نمی تواند کمتر از {length} کاراکتر باشد";
    public static string Pattern(string name, string roles) => $"مقدار فیلد {name} باید دارای شرایط ({roles}) باشد";
}