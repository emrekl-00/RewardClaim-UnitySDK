using System.Linq;
using System.Text;
using RewardClaims;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoinCatcher
{
    /// <summary>
    /// The claim history panel: every claim the SDK saved, newest first, with its status, last error
    /// and attempts, plus a button that retries the unfinished ones. Read-only: it only shows records.
    /// </summary>
    public class ClaimHistory : MonoBehaviour
    {
        [SerializeField] private TMP_Text listLabel;
        [SerializeField] private Button retryUnfinishedButton;
        [Tooltip("Older claims are summed up in one line, so the list fits the panel without scrolling.")]
        [SerializeField] private int maxClaimsShown = 8;

        private void Awake()
        {
            retryUnfinishedButton.onClick.AddListener(ClaimRetries.RetryUnfinishedInBackground);
        }

        // On this panel, so it runs on every open and shows the claims as they are now.
        private void OnEnable()
        {
            RewardClaimsSdk.Claims.ClaimStatusChanged += OnClaimStatusChanged;
            Refresh();
        }

        // The SDK outlives this panel, so a missing unsubscribe would keep calling a destroyed object.
        private void OnDisable()
        {
            if (RewardClaimsSdk.IsInitialized)
                RewardClaimsSdk.Claims.ClaimStatusChanged -= OnClaimStatusChanged;
        }

        private void OnClaimStatusChanged(ClaimRecord claim)
        {
            // Redraw everything: a new claim also changes the order, and the list is short.
            Refresh();
        }

        private void Refresh()
        {
            var claims = RewardClaimsSdk.Claims.Claims;

            // Nothing to retry is worth showing, so the button is greyed out instead of doing nothing.
            retryUnfinishedButton.interactable =
                claims.Any(claim => claim.Status == ClaimStatus.Failed || claim.Status == ClaimStatus.Pending);

            if (claims.Count == 0)
            {
                listLabel.text = "No claims yet. Complete a level!";
                return;
            }

            // The SDK keeps claims in the order they were first made, so newest first is that order reversed.
            var text = new StringBuilder();
            foreach (var claim in claims.Reverse().Take(maxClaimsShown))
                AppendClaim(text, claim);

            var hidden = claims.Count - maxClaimsShown;
            if (hidden > 0)
                text.Append($"<color=#9AA3B5>…and {hidden} older</color>");

            listLabel.text = text.ToString();
        }

        private static void AppendClaim(StringBuilder text, ClaimRecord claim)
        {
            text.Append($"<b>{claim.RewardId}</b>\n");
            text.Append($"<color={StatusColor(claim.Status)}>{claim.Status}</color>");

            // A Pending retry keeps the previous attempt's error, so it shows why the claim is being retried.
            if (claim.LastError != ClaimError.None)
                text.Append($" · {claim.LastError}");

            var attempts = claim.AttemptCount == 1 ? "attempt" : "attempts";
            text.Append($" · {claim.AttemptCount} {attempts} · +{claim.Amount} {claim.ResourceId}\n\n");
        }

        private static string StatusColor(ClaimStatus status)
        {
            switch (status)
            {
                case ClaimStatus.Succeeded:
                    return "#6BE38A";
                case ClaimStatus.Failed:
                    return "#FFA14A";
                case ClaimStatus.Rejected:
                    return "#FF5A6E";
                default:
                    return "#FFD24A";
            }
        }
    }
}
