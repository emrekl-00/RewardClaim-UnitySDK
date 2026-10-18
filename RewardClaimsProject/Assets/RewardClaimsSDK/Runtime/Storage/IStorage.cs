namespace RewardClaims
{
    /// <summary>Saves and loads strings by key; the SDK's only way to reach persistent storage.</summary>
    /// <remarks>
    /// This is the seam that lets tests run without touching disk (<see cref="InMemoryStorage"/>)
    /// and lets a game plug in its own save system or cloud save instead of <see cref="PlayerPrefsStorage"/>.
    /// </remarks>
    public interface IStorage
    {
        /// <summary>Returns the value saved under <paramref name="key"/>, or null when nothing is saved.</summary>
        string Load(string key);

        /// <summary>Saves <paramref name="value"/> under <paramref name="key"/>, replacing any previous value.</summary>
        void Save(string key, string value);
    }
}
