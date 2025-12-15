using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;

namespace _Scripts
{
    public class NumpadButton : MonoBehaviour
    {
        [SerializeField] private HPUIGestureDetector gestureDetector;

        private void OnEnable()
        {
            gestureDetector.TapEvent.AddListener(DoATap);
            gestureDetector.DoubleTapEvent.AddListener(DoDoubleTap);
            gestureDetector.LongPressEvent.AddListener(DoLongPress);
        }

        private void OnDisable()
        {
            gestureDetector.TapEvent.RemoveListener(DoATap);
            gestureDetector.DoubleTapEvent.RemoveListener(DoDoubleTap);
            gestureDetector.LongPressEvent.RemoveListener(DoLongPress);
        }

        private void DoATap(HPUIGestureEventArgs args)
        {
            Debug.Log($"Got a tap for {args.interactableObject.transform.name}");
        }

        private void DoDoubleTap(HPUIGestureEventArgs args)
        {
            Debug.Log($"Got a double tap for {args.interactableObject.transform.name}");
        }

        private void DoLongPress(HPUIGestureEventArgs args)
        {
            Debug.Log($"Got a long press for {args.interactableObject.transform.name}");
        }
    }
}
