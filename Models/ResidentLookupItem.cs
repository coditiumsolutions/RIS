namespace ResidentPortal.Models;

public sealed class ResidentLookupItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string? Tower { get; init; }
    public string? TowerDisplay { get; init; }
    public string? UnitNo { get; init; }
    public string? Block { get; init; }
    public string? Floor { get; init; }
    public string? Street { get; init; }
    public string? Mobile { get; init; }
    public string? MobileMasked { get; init; }
}
