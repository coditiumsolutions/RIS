using ResidentPortal.Models;

namespace ResidentPortal.Services;

public static class SampleDashboardData
{
    private static readonly DateTime CapturedAt = DateTime.Now;

    public static IReadOnlyList<VisitorActivityItem> RecentVisitors { get; } =
    [
        new()
        {
            VisitorName = "Sara Ahmed",
            Purpose = "Family visit",
            Status = "Checked in",
            When = CapturedAt.AddHours(-2)
        },
        new()
        {
            VisitorName = "Usman Ali",
            Purpose = "Package delivery",
            Status = "Checked out",
            When = CapturedAt.AddDays(-1)
        },
        new()
        {
            VisitorName = "Hina Qureshi",
            Purpose = "Maintenance",
            Status = "Expected",
            When = CapturedAt.AddDays(1)
        }
    ];

    public static IReadOnlyList<AdminActivityItem> RecentActivity { get; } =
    [
        new()
        {
            Title = "Resident account reviewed",
            Detail = "Sample event for apartment 12-B",
            When = CapturedAt.AddHours(-3)
        },
        new()
        {
            Title = "Visitor pass issued",
            Detail = "Sample pass for a family visit",
            When = CapturedAt.AddHours(-5)
        },
        new()
        {
            Title = "Portal user signed in",
            Detail = "Sample security event",
            When = CapturedAt.AddDays(-1)
        }
    ];
}
