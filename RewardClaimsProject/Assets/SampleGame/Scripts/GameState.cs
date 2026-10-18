namespace CoinCatcher
{
    /// <summary>Where the game is in its loop. Only one state is active at a time.</summary>
    public enum GameState
    {
        /// <summary>Waiting for the player to press Play.</summary>
        Ready,

        /// <summary>A run is in progress: coins fall and are counted.</summary>
        Playing,

        /// <summary>The player caught enough coins.</summary>
        LevelComplete,

        /// <summary>The player missed too many coins.</summary>
        GameOver,
    }
}
