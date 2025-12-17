using _Scripts.Utils;
using UnityEngine;

namespace _Scripts.ColorPicker
{
    public class ColorWheel : MonoBehaviour
    {
        public enum ColorPickerMode
        {
            RGB,
            HSV,
            Touch
        }

        [Header("Mode")]
        [SerializeField]
        private ColorPickerMode currentMode;

        public ColorPickerMode CurrentMode
        {
            get => currentMode;
            set => currentMode = value;
        }

        [Header("RGB Sliders")]
        [SerializeField] private ColorPickerSlider rSlider;
        [SerializeField] private ColorPickerSlider gSlider;
        [SerializeField] private ColorPickerSlider bSlider;

        [Header("HSV Sliders")]
        [SerializeField] private ColorPickerSlider hSlider;
        [SerializeField] private ColorPickerSlider sSlider;
        [SerializeField] private ColorPickerSlider vSlider;

        [Header("Target")]
        [SerializeField]
        private GameObject targetObject;

        public GameObject TargetObject
        {
            get => targetObject;
            set => targetObject = value;
        }

        [SerializeField]
        private Color targetColor = Color.white;

        public Color TargetColor
        {
            get => targetColor;
            set => targetColor = value;
        }

        private void Update()
        {
            UpdateColor();
        }

        private void UpdateColor()
        {
            switch (currentMode)
            {
                case ColorPickerMode.RGB:
                    UpdateFromRGB();
                    SyncHSVFromColor();
                    SyncTouchFromColor();
                    break;

                case ColorPickerMode.HSV:
                    UpdateFromHSV();
                    SyncRGBFromColor();
                    SyncTouchFromColor();
                    break;

                case ColorPickerMode.Touch:
                    UpdateFromTouch();
                    SyncRGBFromColor();
                    SyncHSVFromColor();
                    break;
            }

            ApplyColorToTarget();
        }

        private void UpdateFromRGB()
        {
            float r = rSlider != null ? rSlider.TargetValue : 0f;
            float g = gSlider != null ? gSlider.TargetValue : 0f;
            float b = bSlider != null ? bSlider.TargetValue : 0f;

            targetColor = new Color(r, g, b, 1f);
        }

        private void UpdateFromHSV()
        {
            float h = hSlider != null ? hSlider.TargetValue : 0f;
            float s = sSlider != null ? sSlider.TargetValue : 0f;
            float v = vSlider != null ? vSlider.TargetValue : 0f;

            targetColor = Color.HSVToRGB(h, s, v);
        }

        private void UpdateFromTouch()
        {

        }

        private void SyncHSVFromColor()
        {
            if (hSlider == null || sSlider == null || vSlider == null)
                return;

            Color.RGBToHSV(targetColor, out float h, out float s, out float v);

            hSlider.TargetValue = h;
            sSlider.TargetValue = s;
            vSlider.TargetValue = v;
        }

        private void SyncRGBFromColor()
        {
            if (rSlider == null || gSlider == null || bSlider == null)
                return;

            rSlider.TargetValue = targetColor.r;
            gSlider.TargetValue = targetColor.g;
            bSlider.TargetValue = targetColor.b;
        }

        private void SyncTouchFromColor()
        {

        }

        private void ApplyColorToTarget()
        {
            if (targetObject == null)
                return;

            HotSwapColor hotSwap = targetObject.GetComponent<HotSwapColor>();
            if (hotSwap != null)
            {
                hotSwap.SetColor(targetColor);
            }
        }
    }
}
