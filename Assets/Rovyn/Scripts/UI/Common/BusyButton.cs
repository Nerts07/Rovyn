using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Button that swaps its label for a spinner and blocks clicks while a request runs.
    public class BusyButton : MonoBehaviour
    {
        public Button button;
        public TMP_Text label;
        public GameObject spinner;

        public bool Busy
        {
            set
            {
                button.interactable = !value;
                label.gameObject.SetActive(!value);
                if (spinner != null) spinner.SetActive(value);
            }
        }
    }
}
