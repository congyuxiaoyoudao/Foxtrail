using UnityEngine;

namespace Hiking.UIUX
{
    public static class TravelHudMotion
    {
        public const float Duration = 0.56f;
        public static Vector3 Evaluate(float time)
        {
            return new Vector3(Ease(time / 0.22f), Ease((time - 0.22f) / 0.18f), Ease((time - 0.4f) / 0.16f));
        }
        private static float Ease(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
