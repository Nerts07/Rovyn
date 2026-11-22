using UnityEngine;

namespace Rovyn.UI
{
    // Hides a panel when this rect gets narrower than a threshold.
    [RequireComponent(typeof(RectTransform))]
    public class ResponsiveHide : MonoBehaviour
    {
        public GameObject target;
        public float minWidth = 1100f;

        bool _visibilityPending;

        void OnEnable() => _visibilityPending = true;
        void OnRectTransformDimensionsChange() => _visibilityPending = true;

        void LateUpdate()
        {
            if (!_visibilityPending) return;
            _visibilityPending = false;
            if (target == null) return;
            bool show = ((RectTransform)transform).rect.width >= minWidth;
            if (target.activeSelf != show) target.SetActive(show);
        }
    }
}
