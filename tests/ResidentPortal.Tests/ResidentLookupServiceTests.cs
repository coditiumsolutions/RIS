using ResidentPortal.Models;
using ResidentPortal.Services;

namespace ResidentPortal.Tests;

public class ResidentLookupServiceTests
{
    private static List<Resident> Seed() =>
    [
        new()
        {
            Uid = 1,
            ResidentName = "Ali Khan",
            Username = "ali.khan",
            CustomerId = "CUST-100",
            Tower = "Tower A",
            UnitNo = "1201",
            Block = "B1",
            Mobile = "03001234567",
            IsActive = true
        },
        new()
        {
            Uid = 2,
            ResidentName = "Sara Ahmed",
            Username = "sara",
            CustomerId = "CUST-200",
            Tower = "Tower Tower B",
            UnitNo = "5",
            Mobile = "03119876543",
            IsActive = true
        },
        new()
        {
            Uid = 3,
            ResidentName = "Inactive Person",
            Username = "inactive",
            CustomerId = "CUST-300",
            UnitNo = "1201",
            IsActive = false
        }
    ];

    [Fact]
    public void FilterActiveMatches_RequiresMinLength_AndOnlyActive()
    {
        var source = Seed().AsQueryable();

        Assert.Empty(ResidentLookupService.FilterActiveMatches(source, "a").ToList());

        var byUnit = ResidentLookupService.FilterActiveMatches(source, "1201").ToList();
        Assert.Single(byUnit);
        Assert.Equal(1, byUnit[0].Uid);

        var byCustomer = ResidentLookupService.FilterActiveMatches(source, "cust-200").ToList();
        Assert.Single(byCustomer);
        Assert.Equal(2, byCustomer[0].Uid);

        var byTower = ResidentLookupService.FilterActiveMatches(source, "tower a").ToList();
        Assert.Single(byTower);
        Assert.Equal(1, byTower[0].Uid);

        var byMobile = ResidentLookupService.FilterActiveMatches(source, "0300123").ToList();
        Assert.Single(byMobile);
        Assert.Equal(1, byMobile[0].Uid);
    }

    [Fact]
    public void FilterActiveMatches_CapsNotAppliedHere_ButOrderingIsCallerOwned()
    {
        var source = Seed().AsQueryable();
        var matches = ResidentLookupService.FilterActiveMatches(source, "cust")
            .OrderBy(r => r.ResidentName)
            .Take(ResidentLookupService.MaxResults)
            .ToList();

        Assert.Equal(2, matches.Count);
        Assert.Equal("Ali Khan", matches[0].ResidentName);
    }

    [Theory]
    [InlineData("03001234567", "******4567")]
    [InlineData("1234", "****")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void MaskMobile_HidesLeadingDigits(string? input, string expected)
    {
        Assert.Equal(expected, ResidentLookupService.MaskMobile(input));
    }
}
