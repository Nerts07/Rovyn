using UnityEngine;

namespace Rovyn.UI
{
    // Loading indicator. Only runs while its GameObject is active.
    public class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 360f;

        void Update() => transform.Rotate(0f, 0f, -degreesPerSecond * Time.unscaledDeltaTime);
    }
}
