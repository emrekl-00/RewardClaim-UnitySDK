using UnityEngine;

namespace RewardClaims
{
    // The SDK's only MonoBehaviour: plain classes get no app lifecycle callbacks, so this hidden object
    // forwards the one we need. Created by RewardClaimsSdk.Initialize in Play mode only.
    internal class RewardClaimsLifecycle : MonoBehaviour
    {
        // Unity also calls OnApplicationPause(false) right after Awake, while Initialize's own retry is
        // still running. Only a resume after a real pause should retry.
        private bool wasPaused;

        public static RewardClaimsLifecycle Create()
        {
            var host = new GameObject("RewardClaimsSdk");

            // Survives scene loads, so the retry-on-resume keeps working in every scene; hidden because
            // it is SDK plumbing, not something the game developer should select or delete.
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideInHierarchy;

            return host.AddComponent<RewardClaimsLifecycle>();
        }

        // On iOS and Android, sending the app to the background pauses it (true) and coming back
        // resumes it (false). A claim may have failed while the phone was offline or asleep, and the
        // OS may have frozen a request mid-flight, so coming back is a good moment to try again.
        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                wasPaused = true;
                return;
            }

            if (!wasPaused)
                return;

            wasPaused = false;
            RewardClaimsSdk.RetryUnfinishedClaimsInBackground();
        }
    }
}
