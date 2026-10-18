using UnityEngine;
using UnityEngine.UI;

namespace CoinCatcher
{
    /// <summary>
    /// The debug "Simulate offline" switch: while on, every claim fails as if the device had no
    /// connection. Switching it off acts like the connection coming back and retries unfinished claims.
    /// </summary>
    public class OfflineToggle : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private Toggle toggle;

        private void Awake()
        {
            // Not saved on purpose: every launch starts online, like a device whose connection is
            // back. That is also what lets the SDK's startup retry finish what failed "offline" in
            // the last session. Without notify, so starting online doesn't trigger a second retry.
            toggle.SetIsOnWithoutNotify(false);
            bootstrap.RewardApi.SimulateOffline = false;

            toggle.onValueChanged.AddListener(OnToggled);
        }

        private void OnToggled(bool offline)
        {
            bootstrap.RewardApi.SimulateOffline = offline;

            // A real game would do this when the OS reports the network is back; here the switch is
            // the "network".
            if (!offline)
                ClaimRetries.RetryUnfinishedInBackground();
        }
    }
}
