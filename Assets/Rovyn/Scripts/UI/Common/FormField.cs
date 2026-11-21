using TMPro;
using UnityEngine;

namespace Rovyn.UI
{
    // Labeled input. Errors replace the label text in red, the way Discord's forms do.
    public class FormField : MonoBehaviour
    {
        public TMP_Text label;
        public TMP_InputField input;
        public string title = "FIELD";
        public bool required = true;

        public string Value => input.text;

        void Awake() => SetError(null);

        public void SetError(string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                label.color = Theme.Interactive;
                label.text = required ? $"{title} <color=#F23F43>*</color>" : title;
            }
            else
            {
                label.color = Theme.ErrorText;
                label.text = $"{title} <i><size=90%>- {error}</size></i>";
            }
        }

        public bool CheckRequired()
        {
            if (required && string.IsNullOrWhiteSpace(input.text))
            {
                SetError("This field is required");
                return false;
            }
            return true;
        }
    }
}
