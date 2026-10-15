using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hiking.Journey
{
    static class PointerUtility
    {
        static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        public static bool OverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            Hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, Hits);
            return Hits.Count > 0;
        }
    }
}
