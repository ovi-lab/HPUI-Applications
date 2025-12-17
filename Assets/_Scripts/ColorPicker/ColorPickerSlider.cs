using UnityEngine;

namespace _Scripts.ColorPicker
{
    public class ColorPickerSlider : MonoBehaviour
    {
        [SerializeField] private string sliderName;
        [SerializeField] private Vector2 _minMax;
        [SerializeField] private Transform _handle;
        [Range(0, 1), SerializeField] private float _targetValue;

        public string SliderName => sliderName;

        public Vector2 MinMax
        {
            get => _minMax;
            set
            {
                _minMax = value;
                UpdateSlider();
            }
        }

        public Transform Handle
        {
            get => _handle;
            set => _handle = value;
        }

        public float TargetValue
        {
            get => _targetValue;
            set
            {
                _targetValue = Mathf.Clamp01(value);
                UpdateSlider();
            }
        }

        private void Update()
        {
            UpdateSlider();
        }

        private void OnValidate()
        {
            UpdateSlider();
        }

        private void UpdateSlider()
        {
            if (_handle == null) return;

            float handleX = Mathf.Lerp(_minMax.x, _minMax.y, _targetValue);

            Vector3 newPosition = _handle.localPosition;
            newPosition.x = handleX;
            _handle.localPosition = newPosition;
        }
    }
}
