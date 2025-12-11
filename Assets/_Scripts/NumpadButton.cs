using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;

namespace _Scripts
{
    public class NumpadButton : MonoBehaviour
    {
        [SerializeField] private HPUIBaseInteractable interactable;

        private void OnEnable()
        {
            interactable.GestureEvent.AddListener(DoSomething);
        }

        private void DoSomething(HPUIGestureEventArgs arg0)
        {
            Debug.Log("Aaa");
        }
    }
}
