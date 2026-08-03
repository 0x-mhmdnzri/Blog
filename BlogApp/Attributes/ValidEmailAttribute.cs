using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace BlogApp.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class ValidEmailAttribute : ValidationAttribute
{
    private static readonly HashSet<string> ValidDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com",
        "googlemail.com",
        "outlook.com",
        "hotmail.com",
        "live.com",
        "msn.com",
        "yahoo.com",
        "icloud.com",
        "me.com",
        "protonmail.com",
        "proton.me",
        "aol.com",
        "zoho.com",
        "yahoo.co.uk",
        "chmail.ir",
        "rocketmail.com",
        "mail.com",
    };

    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            ErrorMessage = "ایمیل الزامی است";
            return false;
        }

        var email = value.ToString()!.Trim();

        if (!EmailRegex.IsMatch(email))
        {
            ErrorMessage = "ایمیل نامعتبر است";
            return false;
        }

        var domain = email[(email.IndexOf('@') + 1)..];

        if (!ValidDomains.Contains(domain))
        {
            ErrorMessage = "لطفا یک ایمیل معتبر وارد کنید.";
            return false;
        }

        return true;
    }
}