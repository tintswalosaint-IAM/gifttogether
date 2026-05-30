// Temporary probe to discover CsCheck Gen API
using CsCheck;
using Xunit;

namespace GiftTogether.Mobile.Tests;

public class ApiProbe
{
    [Fact]
    public void ProbeGenApi()
    {
        // Try Gen.Array
        var genInts = Gen.Int[0, 10];
        var genArr = genInts.Array[0, 5];
        genArr.Sample(arr =>
        {
            Assert.NotNull(arr);
        });
    }
}
