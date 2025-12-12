using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;

namespace _Scripts
{
    public class NumpadButton : MonoBehaviour
    {
        [SerializeField] private HPUIGestureDetector gestureDetector;

        private void OnEnable()
        {
            gestureDetector.TapEvent.AddListener(DoSomething);
        }

        private void DoSomething(HPUIGestureEventArgs args)
        {
            Debug.Log($"Got a tap for {args.interactableObject.transform.name}");
        }
    }
}
