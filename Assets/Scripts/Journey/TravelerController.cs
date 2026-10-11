using System;
using System.Collections;
using UnityEngine;
namespace Hiking.Journey
{
    public class TravelerController : MonoBehaviour
    {
        public Transform visual;
        public bool IsMoving { get; private set; }
        public float Progress01 { get; private set; }
        public bool Paused { get; set; }
        public float MoveFraction { get; private set; }
        public float MoveDirection { get; private set; }
        Coroutine movement;
        JourneyMap map;
        float? transitionInset;
        public void SetTransitionInset(float? inset) => transitionInset = inset;
        public void PlaceOnRoute(JourneyMap route)
        {
            CancelMovement(); map = route; Progress01 = 0;
            transform.SetParent(map.transform, false); RefreshPosition();
        }
        // 读档：把狐狸放回路线上并设到保存的进度。此刻不启动行走——
        // 是否继续走由 GameFlowController 按存档里的流程阶段决定
        // （策划的退出状态表：两格之间行走的，从保存位置继续走）。
        public void RestorePosition(JourneyMap route, float progress01)
        {
            if (route == null) return;
            CancelMovement();
            map = route;
            transform.SetParent(map.transform, false);
            Progress01 = Mathf.Clamp01(progress01);
            RefreshPosition();
        }
        public void RefreshPosition()
        {
            if (map != null) transform.position = map.ProgressPoint(Progress01);
        }
        public void FaceCamera(Camera camera)
        {
            transform.rotation = camera.transform.rotation;
            if (visual == null) return;
            visual.localPosition = new Vector3(transitionInset ?? 0, 0, 0);
        }
        public void MoveToProgress(float target, float duration, Action arrived)
        {
            if (IsMoving || map == null) return;
            movement = StartCoroutine(Walk(Mathf.Clamp01(target), duration, arrived));
        }
        IEnumerator Walk(float target, float duration, Action arrived)
        {
            IsMoving = true;
            float start = Progress01;
            MoveFraction = 0; MoveDirection = Mathf.Sign(target - start);
            float elapsed = 0;
            double previousTime = Time.realtimeSinceStartupAsDouble;
            yield return null;
            while (elapsed < Mathf.Max(.1f, duration) || Paused)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                float delta = Mathf.Max(0, (float)(now - previousTime));
                previousTime = now;
                if (Paused) { yield return null; continue; }
                elapsed += delta;
                MoveFraction = Mathf.Clamp01(elapsed / Mathf.Max(.1f, duration));
                Progress01 = Mathf.Lerp(start, target, MoveFraction);
                RefreshPosition();
                // Finish on the arrival frame so the next step starts without an idle frame.
                if (MoveFraction >= 1) break;
                yield return null;
            }
            IsMoving = false; movement = null; arrived?.Invoke();
        }
        public void CancelMovement()
        {
            if (movement != null) StopCoroutine(movement);
            movement = null; IsMoving = false;
            transitionInset = null;
            if (visual != null) visual.localPosition = Vector3.zero;
        }
        void OnDisable() => CancelMovement();
    }
}

