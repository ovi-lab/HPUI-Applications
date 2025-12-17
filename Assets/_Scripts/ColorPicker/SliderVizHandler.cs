using System.Collections.Generic;
using EditorAttributes;
using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;

namespace _Scripts.ColorPicker
{
    public class SliderVizHandler : MonoBehaviour
    {
        [SerializeField] private List<GameObject> RGBSliders;
        [SerializeField] private List<GameObject> HSVSliders;
        // TODO: Implement the callbacks and stuff for the all on-finger color picker
        [SerializeField] private GameObject ColorPickerPanel;

        [SerializeField] private ColorWheel wheel;
        private int counter;

        private void OnEnable()
        {
            counter = 0;
            foreach (var item in RGBSliders)
            {
                item.GetComponent<HPUIGeneratedContinuousInteractable>().ContinuousSurfaceEvent.AddListener(WaitForApproximators);
                counter++;
            }
            foreach (var item in HSVSliders)
            {
                item.GetComponent<HPUIGeneratedContinuousInteractable>().ContinuousSurfaceEvent.AddListener(WaitForApproximators);
                counter++;
            }
        }

        private void WaitForApproximators(HPUIContinuousSurfaceCreatedEventArgs args)
        {
            counter--;

            if (counter <= 0)
            {
                SetActiveVisualization();
            }
        }

        [Button]
        public void SetActiveVisualization()
        {
            switch (wheel.CurrentMode)
            {
                case ColorWheel.ColorPickerMode.RGB:
                    SetVisibility(true, ColorWheel.ColorPickerMode.RGB);
                    SetVisibility(false, ColorWheel.ColorPickerMode.HSV);
                    SetVisibility(false, ColorWheel.ColorPickerMode.Touch);
                    break;
                case ColorWheel.ColorPickerMode.HSV:
                    SetVisibility(false, ColorWheel.ColorPickerMode.RGB);
                    SetVisibility(true, ColorWheel.ColorPickerMode.HSV);
                    SetVisibility(false, ColorWheel.ColorPickerMode.Touch);
                    break;
                case ColorWheel.ColorPickerMode.Touch:
                    SetVisibility(false, ColorWheel.ColorPickerMode.RGB);
                    SetVisibility(false, ColorWheel.ColorPickerMode.HSV);
                    SetVisibility(true, ColorWheel.ColorPickerMode.Touch);
                    break;
                default:
                    break;
            }
        }

        private void SetVisibility(bool state, ColorWheel.ColorPickerMode target)
        {

            switch (target)
            {
                case ColorWheel.ColorPickerMode.RGB:
                    foreach (GameObject item in RGBSliders)
                    {
                        item.SetActive(state);
                    }
                    break;
                case ColorWheel.ColorPickerMode.HSV:
                    foreach (GameObject item in HSVSliders)
                    {
                        item.SetActive(state);
                    }
                    break;
                case ColorWheel.ColorPickerMode.Touch:
                    // TODO: Implement
                    break;
                default:
                    break;
            }
        }

    }
}
