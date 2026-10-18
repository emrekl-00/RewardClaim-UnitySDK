using System;

namespace RewardClaims
{
    // One saved balance. JsonUtility can't serialize a Dictionary, so PlayerProgress keeps a list of these.
    // Public fields because JsonUtility only reads public or [SerializeField] fields, never properties.
    [Serializable]
    internal class ResourceBalance
    {
        public string resourceId;
        public int balance;
    }
}
