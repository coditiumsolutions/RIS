namespace ResidentPortal.Helpers;

/// <summary>
/// Formats resident tower/unit labels for Staff Residents list display.
/// Does not prepend "Tower"; collapses adjacent repeated "Tower" words when present.
/// </summary>
public static class TowerDisplay
{
    public static string Format(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value ?? string.Empty;

        var tokens = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var hasTowerWord = false;
        foreach (var token in tokens)
        {
            if (token.Equals("tower", StringComparison.OrdinalIgnoreCase))
            {
                hasTowerWord = true;
                break;
            }
        }

        if (!hasTowerWord)
            return value;

        var parts = new List<string>(tokens.Length);
        foreach (var token in tokens)
        {
            if (token.Equals("tower", StringComparison.OrdinalIgnoreCase)
                && parts.Count > 0
                && parts[^1].Equals("tower", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            parts.Add(token);
        }

        return string.Join(' ', parts);
    }
}
