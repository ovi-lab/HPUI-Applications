using System;
using UnityEngine;

namespace _Scripts.HPUI
{
    public class GestureStateMachine
    {
        private float alpha;
        private float beta;
        private readonly float? prematureTriggerTime; // null = disabled

        public bool GestureOngoing { get; private set; }
        public bool PrematureTrigger { get; private set; }
        public int TickCount { get; private set; }
        public float GestureDuration { get; private set; }

        private float timeSinceLastInput;

        public event Action OnGestureStarted;
        public event Action OnPrematureTriggerReached;
        public event Action OnGestureCompleted;
        public event Action OnGestureCancelled;

        public GestureStateMachine(float alpha, float beta, float? prematureTriggerTime = null)
        {
            this.alpha = alpha;
            this.beta = beta;
            this.prematureTriggerTime = prematureTriggerTime;
            WarnIfAlphaExceedsBeta(alpha, beta);
        }

        /// <summary>
        /// Updates alpha/beta thresholds. Safe mid-gesture: alpha affects the
        /// validity check and beta the idle timeout, both applied live.
        /// </summary>
        public void UpdateThresholds(float alpha, float beta)
        {
            Debug.Assert(!float.IsNaN(alpha) && !float.IsNaN(beta),
                $"Non-finite thresholds: alpha={alpha}, beta={beta}");
            this.alpha = Mathf.Max(0f, alpha);
            this.beta = Mathf.Max(0f, beta);
            WarnIfAlphaExceedsBeta(this.alpha, this.beta);
        }

        /// <summary>
        /// A gesture always ends with duration >= beta, so alpha > beta makes
        /// every gesture invalid - a configuration that can never complete.
        /// </summary>
        private static void WarnIfAlphaExceedsBeta(float alpha, float beta)
        {
            if (alpha > beta)
                Debug.LogWarning($"GestureStateMachine: alpha ({alpha}) > beta ({beta}); every gesture will be cancelled instead of completing.");
        }

        /// <summary>
        /// Ends the ongoing gesture as if the idle timeout had elapsed, applying
        /// the same valid/invalid split as <see cref="Tick"/>. No-op when idle.
        /// </summary>
        public void EndIfOngoing()
        {
            if (!GestureOngoing)
                return;

            bool wasPrematureTrigger = PrematureTrigger;
            bool valid = GestureDuration >= alpha;
            Reset();
            if (wasPrematureTrigger)
                return; // matches Tick: premature-triggered gestures end silently
            if (valid)
                OnGestureCompleted?.Invoke();
            else
                OnGestureCancelled?.Invoke();
        }

        public void ReportInput()
        {
            if (!GestureOngoing)
            {
                GestureOngoing = true;
                OnGestureStarted?.Invoke();
            }

            TickCount++;
            timeSinceLastInput = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!GestureOngoing)
                return;

            timeSinceLastInput += deltaTime;
            GestureDuration += deltaTime;

            if (!PrematureTrigger && prematureTriggerTime.HasValue && GestureDuration >= prematureTriggerTime.Value)
            {
                PrematureTrigger = true;
                OnPrematureTriggerReached?.Invoke();
            }

            if (timeSinceLastInput >= beta)
            {
                if (PrematureTrigger)
                {
                    // already delivered via the premature trigger - end silently, no further events
                    Reset();
                }
                else
                {
                    bool valid = GestureDuration >= alpha;
                    Reset();
                    if (valid)
                        OnGestureCompleted?.Invoke();
                    else
                        OnGestureCancelled?.Invoke();
                }
            }
        }

        public void Cancel()
        {
            if (!GestureOngoing)
                return;
            bool wasPrematureTrigger = PrematureTrigger;
            Reset();
            if (!wasPrematureTrigger)
                OnGestureCancelled?.Invoke();
        }

        public void Reset()
        {
            GestureOngoing = false;
            PrematureTrigger = false;
            TickCount = 0;
            GestureDuration = 0f;
            timeSinceLastInput = 0f;
        }
    }
}
