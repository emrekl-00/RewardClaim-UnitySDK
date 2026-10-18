using System.Collections.Generic;
using UnityEngine;

namespace CoinCatcher
{
    /// <summary>All levels in play order. An asset, so designers tune difficulty without touching code.</summary>
    [CreateAssetMenu(menuName = "Coin Catcher/Level List", fileName = "Levels")]
    public class LevelList : ScriptableObject
    {
        [SerializeField] private List<LevelSettings> levels = new List<LevelSettings>();

        public int Count => levels.Count;

        public LevelSettings this[int index] => levels[index];
    }
}
