using UnityEngine;

namespace CoinCatcher
{
    /// <summary>
    /// Shrinks a full-screen UI panel to <see cref="Screen.safeArea"/>, so notches, camera holes and
    /// rounded corners never cover the HUD. Put the HUD's content under this panel.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform panel;
        private Rect appliedArea;
        private Vector2Int appliedScreenSize;

        private void Awake()
        {
            panel = (RectTransform)transform;
            Apply();
        }

        // The safe area changes on rotation, on foldables and when the Device Simulator switches
        // phones, so check every frame. It is a cheap comparison; the layout is only rebuilt on change.
        private void Update()
        {
            if (Screen.safeArea != appliedArea
                || Screen.width != appliedScreenSize.x
                || Screen.height != appliedScreenSize.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            appliedArea = Screen.safeArea;
            appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            // Screen.safeArea is in pixels; anchors are 0..1 fractions of the parent (the full-screen
            // canvas). Setting the anchors and zeroing the offsets makes the panel exactly the safe area,
            // whatever the Canvas Scaler does to the canvas size.
            var min = appliedArea.position;
            var max = appliedArea.position + appliedArea.size;
            panel.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            panel.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }
    }
}
