using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Rovyn.UI
{
    // Tab / Shift+Tab moves between the input fields of a form.
    public class TabFocus : MonoBehaviour
    {
        public TMP_InputField[] fields;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.tabKey.wasPressedThisFrame || fields.Length == 0) return;

            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int index = System.Array.FindIndex(fields, f => f != null && f.gameObject == current);
            if (index < 0) return;

            int step = keyboard.shiftKey.isPressed ? -1 : 1;
            for (int i = 1; i <= fields.Length; i++)
            {
                var next = fields[(index + step * i + fields.Length * 2) % fields.Length];
                if (next == null || !next.gameObject.activeInHierarchy || !next.interactable) continue;
                next.Select();
                next.ActivateInputField();
                return;
            }
        }
    }
}
