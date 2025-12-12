using System.Collections;
using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;

namespace _Scripts
{
    public class HPUIGestureDetector : MonoBehaviour
    {
        [Header("Tap Event Parameters")]
        [SerializeField] private float tapEventDuration;
        [SerializeField] private float tapTimeoutDuration;
        [SerializeField] private float tapDistanceLimit;

        private bool isGestureActive = false;

        private bool tapInvalid = false;
        private IHPUIInteractable currentInteractable = null;
        private Coroutine tapTimeoutCoroutine;

        [SerializeField] private HPUITapEvent tapEvent = new HPUITapEvent();

        public HPUITapEvent TapEvent
        {
            get => tapEvent;
            set => tapEvent = value;
        }

        private void OnEnable()
        {
            HPUIBaseInteractable[] interactables = Object.FindObjectsByType<HPUIBaseInteractable>(FindObjectsSortMode.None);

            foreach (var interactable in interactables)
            {
                interactable.GestureEvent.AddListener(OnTap);
            }
        }

        private void OnTap(HPUIGestureEventArgs args)
        {

            if (isGestureActive)
            {
                if (args.interactableObject != currentInteractable)
                {
                    tapInvalid = true;
                }
            }
            else
            {
                currentInteractable = args.interactableObject;
                isGestureActive = true;
            }

            if (tapTimeoutCoroutine != null) StopCoroutine(tapTimeoutCoroutine);
            tapTimeoutCoroutine = StartCoroutine(TapTimeout(args, tapEventDuration, tapTimeoutDuration, tapDistanceLimit, tapInvalid));
        }

        private IEnumerator TapTimeout(HPUIGestureEventArgs args, float tapEventDuration, float tapTimeoutDuration, float tapDistanceLimit, bool tapInvalid)
        {
            yield return new WaitForSeconds(tapTimeoutDuration);

            // checking if contact with the current interactable was never broken
            // the duration is greater than the minimum duration
            // the total distance and total magnitude is lesser than the distance tapDistanceLimit
            if (!tapInvalid && args.TimeDelta < tapEventDuration && args.CumulativeDirection.magnitude < tapDistanceLimit && args.CumulativeDistance < tapDistanceLimit)
            {
                TapEvent?.Invoke(args);
                // Debug.Log($"Tap Invoked for Interactable {args.interactableObject.transform.name}");
            }
            // else
            // {
            // Debug.Log($"Tap event was rejected! \n TapInvalid: {tapInvalid} \n Duration: {args.TimeDelta}, \n Cumulative Direction: {args.CumulativeDirection.magnitude}, \n Cumulative Distance: {args.CumulativeDistance}");
            // }
            isGestureActive = false;
            tapInvalid = false;
            currentInteractable = null;
        }
    }

    public class CurrentGestureData
    { }

    public class HPUITapEvent : HPUIGestureEvent
    { }
}
