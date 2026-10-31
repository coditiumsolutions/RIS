using ResidentPortal.Helpers;

namespace ResidentPortal.Tests;

public class TowerDisplayTests
{
    [Theory]
    [InlineData("Tower A", "Tower A")]
    [InlineData("tower a", "tower a")]
    [InlineData("Tower Tower A", "Tower A")]
    [InlineData("Block B", "Block B")]
    [InlineData("A", "A")]
    public void Format_MatchesApprovedCases(string input, string expected)
    {
        Assert.Equal(expected, TowerDisplay.Format(input));
    }
}
