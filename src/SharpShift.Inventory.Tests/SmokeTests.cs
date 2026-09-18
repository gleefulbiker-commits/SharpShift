using NUnit.Framework;

namespace SharpShift.Inventory.Tests;

public class SmokeTests
{
    [Test]
    public void TestInfrastructureIsConfigured()
    {
        Assert.That(true, Is.True);
    }
}
