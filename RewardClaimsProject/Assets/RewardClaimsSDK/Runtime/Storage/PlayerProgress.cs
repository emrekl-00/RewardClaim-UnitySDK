using System;
using System.Collections.Generic;

namespace RewardClaims
{
    // Everything the SDK saves for one player, written as a single JSON document.
    // Claims join this same document in Step 8, so "reward added + claim succeeded" is one write
    // and a crash can never save one without the other.
    [Serializable]
    internal class PlayerProgress
    {
        public List<ResourceBalance> balances = new List<ResourceBalance>();
    }
}
