using UnityEngine;

namespace Rovyn.UI
{
    // Hides a panel when this rect gets narrower than a threshold, like Discord dropping
    // the Active Now column on small windows. Reacts to layout changes, no polling.
    [RequireComponent(typeof(RectTransform))]
    public class ResponsiveHide : MonoBehaviour
    {
        public GameObject target;
        public float minWidth = 1100f;

        void OnEnable() => Apply();
        void OnRectTransformDimensionsChange() => Apply();

        void Apply()
        {
            if (target == null) return;
            bool show = ((RectTransform)transform).rect.width >= minWidth;
            if (target.activeSelf != show) target.SetActive(show);
        }
    }
}
