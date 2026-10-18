using UnityEngine;

namespace Hiking.UIUX
{
    [ExecuteAlways]
    public sealed class TravelCameraLens : MonoBehaviour
    {
        public UnityEngine.UI.RawImage Shutter;
        public RectTransform Ring;
        public Material Template;
        private Material instance;
        private float progress;

        private void OnEnable() { Apply(progress); }
        public void Apply(float value)
        {
            progress = Mathf.Clamp01(value);
            if (Ring) Ring.localRotation = Quaternion.Euler(0, 0, -28 * progress);
            if (!Shutter) return;
            // Keep serialized prefab references on the persistent template, never on a transient clone.
            if (!Application.isPlaying) { Shutter.material = Template; return; }
            if (!instance && Template)
            {
                instance = new Material(Template) { hideFlags = HideFlags.HideAndDontSave };
                Shutter.material = instance;
            }
            if (instance) instance.SetFloat("_Progress", progress);
        }
        private void OnDisable()
        {
            if (!instance) return;
            if (Shutter) Shutter.material = Template;
            if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
            instance = null;
        }
    }
}
