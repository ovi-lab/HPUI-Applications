using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Keyboard
{
    public class KeyboardRawInputDisplay : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI tmp;

        [SerializeField]
        private Image panel;

        [SerializeField]
        private KeyboardInputCapture capture;

        private bool active = false;

        private void OnEnable()
        {
            capture.OnKeyboardInputCaptureRaw.AddListener(UpdateTmp);
        }

        private void OnDisable()
        {
            capture.OnKeyboardInputCaptureRaw.RemoveListener(UpdateTmp);
        }

        private void UpdateTmp(Vector2 rawInput, string name)
        {
            tmp.text = $"{name}\n{rawInput.x}, {rawInput.y}";
            panel.color = Color.green;
            active = true;
        }

        private void LateUpdate()
        {
            if (!active)
            {
                panel.color = Color.red;
            }
            else
            {
                active = false;
            }
        }
    }
}
