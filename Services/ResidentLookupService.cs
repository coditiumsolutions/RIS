using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Helpers;
using ResidentPortal.Models;

namespace ResidentPortal.Services;

/// <summary>
/// Active-resident typeahead lookup for Staff Walk-in (and any other Staff resident pickers).
/// Residents have no CNIC column; search covers name, username, Customer ID, tower/unit/block/street/floor, mobile.
/// </summary>
public sealed class ResidentLookupService
{
    public const int MinQueryLength = 2;
    public const int MaxResults = 15;

    private readonly ApplicationDbContext _db;

    public ResidentLookupService(ApplicationDbContext db)
    {
        _db = db;
    }

    public static IQueryable<Resident> FilterActiveMatches(IQueryable<Resident> source, string rawQuery)
    {
        var term = (rawQuery ?? string.Empty).Trim().ToLowerInvariant();
        if (term.Length < MinQueryLength)
            return source.Where(_ => false);

        return source.Where(r =>
            r.IsActive
            && (r.ResidentName.ToLower().Contains(term)
                || r.Username.ToLower().Contains(term)
                || r.CustomerId.ToLower().Contains(term)
                || (r.UnitNo != null && r.UnitNo.ToLower().Contains(term))
                || (r.Tower != null && r.Tower.ToLower().Contains(term))
                || (r.Block != null && r.Block.ToLower().Contains(term))
                || (r.Street != null && r.Street.ToLower().Contains(term))
                || (r.Floor != null && r.Floor.ToLower().Contains(term))
                || (r.Mobile != null && r.Mobile.Contains(term))
                || (r.Mobile2 != null && r.Mobile2.Contains(term))));
    }

    public async Task<IReadOnlyList<ResidentLookupItem>> SearchAsync(string? q, CancellationToken ct = default)
    {
        var term = (q ?? string.Empty).Trim();
        if (term.Length < MinQueryLength)
            return [];

        var rows = await FilterActiveMatches(_db.Residents.AsNoTracking(), term)
            .OrderBy(r => r.ResidentName)
            .ThenBy(r => r.Uid)
            .Take(MaxResults)
            .Select(r => new
            {
                r.Uid,
                r.ResidentName,
                r.CustomerId,
                r.Tower,
                r.UnitNo,
                r.Block,
                r.Floor,
                r.Street,
                r.Mobile
            })
            .ToListAsync(ct);

        return rows.Select(r => Map(
            r.Uid,
            r.ResidentName,
            r.CustomerId,
            r.Tower,
            r.UnitNo,
            r.Block,
            r.Floor,
            r.Street,
            r.Mobile)).ToList();
    }

    public async Task<ResidentLookupItem?> GetActiveAsync(int id, CancellationToken ct = default)
    {
        var r = await _db.Residents.AsNoTracking()
            .Where(x => x.Uid == id && x.IsActive)
            .Select(x => new
            {
                x.Uid,
                x.ResidentName,
                x.CustomerId,
                x.Tower,
                x.UnitNo,
                x.Block,
                x.Floor,
                x.Street,
                x.Mobile
            })
            .SingleOrDefaultAsync(ct);

        return r is null
            ? null
            : Map(r.Uid, r.ResidentName, r.CustomerId, r.Tower, r.UnitNo, r.Block, r.Floor, r.Street, r.Mobile);
    }

    public static string MaskMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile))
            return string.Empty;

        var digits = new string(mobile.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return "****";

        return new string('*', Math.Min(digits.Length - 4, 6)) + digits[^4..];
    }

    private static ResidentLookupItem Map(
        int id,
        string name,
        string customerId,
        string? tower,
        string? unitNo,
        string? block,
        string? floor,
        string? street,
        string? mobile)
    {
        var towerFormatted = TowerDisplay.Format(tower);
        return new ResidentLookupItem
        {
            Id = id,
            Name = name,
            CustomerId = customerId,
            Tower = tower,
            TowerDisplay = string.IsNullOrWhiteSpace(towerFormatted) ? null : towerFormatted,
            UnitNo = unitNo,
            Block = block,
            Floor = floor,
            Street = street,
            Mobile = mobile,
            MobileMasked = string.IsNullOrWhiteSpace(mobile) ? null : MaskMobile(mobile)
        };
    }
}
