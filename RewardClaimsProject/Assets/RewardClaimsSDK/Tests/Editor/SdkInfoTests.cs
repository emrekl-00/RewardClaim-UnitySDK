using NUnit.Framework;

namespace RewardClaims.Tests
{
    public class SdkInfoTests
    {
        // Compiles only if the test assembly can see RewardClaims.Runtime, which is the real point.
        [Test]
        public void Version_IsNotEmpty()
        {
            Assert.That(SdkInfo.Version, Is.Not.Null.And.Not.Empty);
        }
    }
}
