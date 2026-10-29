namespace ResidentPortal.Models;

public class ResidentProfileViewModel
{
    public string ResidentName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Mobile { get; set; } = string.Empty;

    public string? UnitNo { get; set; }

    public string? Street { get; set; }

    public string? Tower { get; set; }

    public string? Floor { get; set; }

    public string? Block { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool HasPropertyDetails =>
        !string.IsNullOrWhiteSpace(UnitNo)
        || !string.IsNullOrWhiteSpace(Street)
        || !string.IsNullOrWhiteSpace(Tower)
        || !string.IsNullOrWhiteSpace(Floor)
        || !string.IsNullOrWhiteSpace(Block);
}

public class VisitorActivityItem
{
    public string VisitorName { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime When { get; set; }
}

public class ResidentDashboardViewModel
{
    public ResidentProfileViewModel Profile { get; set; } = new();

    public IReadOnlyList<VisitorActivityItem> RecentVisitors { get; set; } = [];
}

public class AdminActivityItem
{
    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public DateTime When { get; set; }
}

public class AdminDashboardViewModel
{
    public string AdminName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public int ResidentCount { get; set; }

    public int VisitorsTodayCount { get; set; }

    public int PortalUserCount { get; set; }

    public IReadOnlyList<AdminActivityItem> RecentActivity { get; set; } = [];
}
