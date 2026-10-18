namespace RewardClaims.Tests
{
    // Builds wallets the way the SDK will: on top of a ProgressStore.
    internal static class TestWallets
    {
        public const string PlayerId = "player-1";
        public const string ProgressKey = "RewardClaims.Progress." + PlayerId;

        // Fresh storage each time, so tests that don't care about saving start from starting balances.
        public static ResourceWallet Create(params ResourceDefinition[] resources)
        {
            return Create(new InMemoryStorage(), resources);
        }

        // Pass the same storage again to simulate an app restart.
        public static ResourceWallet Create(IStorage storage, params ResourceDefinition[] resources)
        {
            return new ResourceWallet(resources, new ProgressStore(storage, PlayerId));
        }
    }
}
