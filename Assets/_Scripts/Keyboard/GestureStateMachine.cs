using System;

namespace _Scripts.Keyboard
{
    public class GestureStateMachine
    {
        private readonly float alpha;
        private readonly float beta;
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
