using System.Text.RegularExpressions;

namespace ResidentPortal.Tests;

public class LoginAuthLinqSanityTests
{
    private static readonly string[] RawSqlMarkers =
    [
        "FromSqlRaw",
        "FromSqlInterpolated",
        "FromSql<",
        "ExecuteSqlRaw",
        "ExecuteSqlInterpolated",
        "ExecuteSql(",
        "SqlQueryRaw",
        "SqlQuery<",
        "Database.SqlQuery",
        "SqlConnection",
        "SqlCommand",
        "new SqlParameter"
    ];

    [Fact]
    public void PortalAuthentication_UsesOnlyEfCoreLinq_NoRawSql()
    {
        var source = ReadRepoFile("Services/PortalAuthentication.cs");

        Assert.Contains("SingleOrDefaultAsync", source);
        Assert.Contains("ValidateResidentAsync", source);
        Assert.Contains("ValidateStaffAsync", source);
        Assert.DoesNotContain("FromSql", source, StringComparison.Ordinal);

        foreach (var marker in RawSqlMarkers)
            Assert.DoesNotContain(marker, source, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidentAndStaffLoginActions_CallEfAuthHelpers_NotRawSql()
    {
        var residentLogin = ReadRepoFile("Controllers/HomeController.cs");
        var staffLogin = ReadRepoFile("Areas/Staff/Controllers/AccountController.cs");

        Assert.Contains("ValidateResidentAsync", residentLogin);
        Assert.Contains("ValidateStaffAsync", staffLogin);

        foreach (var source in new[] { residentLogin, staffLogin })
        {
            foreach (var marker in RawSqlMarkers)
                Assert.DoesNotContain(marker, source, StringComparison.Ordinal);

            Assert.False(
                Regex.IsMatch(source, @"\bSELECT\b.+\bFROM\b", RegexOptions.IgnoreCase | RegexOptions.Singleline),
                "Login controllers must not embed raw SQL strings.");
        }
    }

    private static string ReadRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            var csproj = Path.Combine(dir.FullName, "ResidentPortal.csproj");
            if (File.Exists(csproj))
            {
                candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo file '{relativePath}' from {AppContext.BaseDirectory}.");
    }
}
