using System;
using System.Collections.Generic;

namespace RewardClaims
{
    // Everything the SDK saves for one player, written as a single JSON document.
    // Balances and claims share it, so "reward added + claim succeeded" is one write
    // and a crash can never save one without the other.
    [Serializable]
    internal class PlayerProgress
    {
        public List<ResourceBalance> balances = new List<ResourceBalance>();
        public List<ClaimRecord> claims = new List<ClaimRecord>();
    }
}
