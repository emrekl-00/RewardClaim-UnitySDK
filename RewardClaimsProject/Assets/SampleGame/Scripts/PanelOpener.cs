using UnityEngine;
using UnityEngine.UI;

namespace CoinCatcher
{
    /// <summary>
    /// Shows a pop-up panel when one of its open buttons is clicked and hides it on Close. Lives on an
    /// always-active object: an inactive panel's own scripts get no Awake, so they couldn't wire this.
    /// </summary>
    public class PanelOpener : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [Tooltip("e.g. a Shop button on several screens.")]
        [SerializeField] private Button[] openButtons;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            foreach (var button in openButtons)
                button.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);

            panel.SetActive(false);
        }

        // The panel's scripts refresh in their OnEnable, so each open shows the state as it is now.
        private void Open()
        {
            panel.SetActive(true);
        }

        private void Close()
        {
            panel.SetActive(false);
        }
    }
}
