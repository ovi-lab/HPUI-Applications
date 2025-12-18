using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;

namespace _Scripts.ColorPicker
{
    /// <summary>
    /// This is a pretty hacky script, 
    /// we should simplify the process 
    /// of getting gesture data from 
    /// continuous interactables down the line
    public class HPUISlider : MonoBehaviour
    {
        [SerializeField] private ColorPickerSlider slider;
        [SerializeField] private Vector2 minMax;
        [SerializeField] private bool alongX;
        [SerializeField] private float targetVal;

        private HPUIGeneratedContinuousInteractable interactable;

        private void OnEnable()
        {
            interactable = GetComponent<HPUIGeneratedContinuousInteractable>();

            interactable.GestureEvent.AddListener(UpdateSliderHandle);
        }

        private void OnDisable()
        {
            interactable.GestureEvent.RemoveListener(UpdateSliderHandle);
        }

        private void UpdateSliderHandle(HPUIGestureEventArgs args)
        {
            targetVal = Mathf.InverseLerp(minMax.x, minMax.y, alongX ? args.Position.x : args.Position.y);
            slider.TargetValue = targetVal;
        }
    }
}
