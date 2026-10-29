using System.ComponentModel.DataAnnotations;

namespace ResidentPortal.Models;

public sealed class AllowedDurationAttribute : ValidationAttribute
{
    public AllowedDurationAttribute()
        : base("Please select a valid visit duration.")
    {
    }

    public override bool IsValid(object? value)
    {
        return value is string duration && VisitorDuration.IsAllowed(duration);
    }
}
