using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Rovyn.UI
{
    // Short fade + scale for popups and overlays.
    [RequireComponent(typeof(CanvasGroup))]
    public class FadePanel : MonoBehaviour
    {
        public RectTransform content; // scaled slightly while fading, optional
        public float duration = 0.12f;

        CanvasGroup _group;
        int _version;

        public bool IsOpen { get; private set; }

        CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        public void Show()
        {
            IsOpen = true;
            if (!gameObject.activeSelf)
            {
                Group.alpha = 0f;
                gameObject.SetActive(true);
            }
            transform.SetAsLastSibling();
            AnimateAsync(true).Forget();
        }

        public void Hide()
        {
            IsOpen = false;
            if (!gameObject.activeSelf) return;
            AnimateAsync(false).Forget();
        }

        async UniTaskVoid AnimateAsync(bool show)
        {
            int version = ++_version;
            var group = Group;
            group.blocksRaycasts = show;
            group.interactable = show;
            float from = group.alpha, to = show ? 1f : 0f;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                group.alpha = Mathf.Lerp(from, to, k);
                if (content != null) content.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, group.alpha);
                await UniTask.Yield();
                if (this == null || version != _version) return;
            }

            group.alpha = to;
            if (content != null) content.localScale = Vector3.one;
            if (!show) gameObject.SetActive(false);
        }
    }
}
