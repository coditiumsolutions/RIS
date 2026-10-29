using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ResidentPortal.Models;

public sealed class ContactPhoneAttribute : ValidationAttribute
{
    public ContactPhoneAttribute()
        : base("Enter a valid phone number (7 to 15 digits; +, -, and spaces are allowed).")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        var text = Convert.ToString(value);
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var digits = 0;
        foreach (var c in text)
        {
            if (char.IsDigit(c))
            {
                digits++;
                continue;
            }

            if (c is '+' or '-' or ' ')
                continue;

            return false;
        }

        return digits is >= 7 and <= 15;
    }
}

public sealed class CnicAttribute : ValidationAttribute
{
    private static readonly Regex CnicPattern = new(@"^(\d{13}|\d{5}-\d{7}-\d)$", RegexOptions.Compiled);

    public CnicAttribute()
        : base("Enter a valid CNIC (13 digits, or #####-#######-#).")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        var text = Convert.ToString(value)?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return true;

        return CnicPattern.IsMatch(text);
    }
}

public sealed class PakistaniMobileAttribute : ValidationAttribute
{
    private static readonly Regex MobilePattern = new(@"^03\d{9}$", RegexOptions.Compiled);

    public PakistaniMobileAttribute()
        : base("Enter a valid Pakistani mobile number (03XXXXXXXXX).")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        var text = Convert.ToString(value)?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return true;

        return MobilePattern.IsMatch(text);
    }
}
