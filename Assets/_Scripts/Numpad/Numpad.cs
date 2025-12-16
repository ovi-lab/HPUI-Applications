using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace _Scripts
{
    public class Numpad : MonoBehaviour
    {
        [SerializeField] private List<NumpadButton> buttons;
        [SerializeField] private TextMeshProUGUI numpadText;

        private void OnEnable()
        {
            foreach (Transform child in transform)
            {
                NumpadButton button = child.gameObject.GetComponent<NumpadButton>();
                buttons.Add(button);
                button.OnTap.AddListener(OnButtonTap);
                button.OnDoubleTap.AddListener(OnDoubleTap);
                button.OnLongPress.AddListener(OnLongPress);
            }
        }

        private void OnButtonTap(NumpadButton button)
        {
            if (button.DoubleTapMode)
            {
                switch (button.DoubleTapAction)
                {
                    case "delete":
                        numpadText.text = string.IsNullOrWhiteSpace(numpadText.text) ? numpadText.text : numpadText.text.Substring(0, numpadText.text.Length - 1);
                        break;
                    case "clear":
                        numpadText.text = "";
                        break;
                    case "reverse":
                        numpadText.text = new String(numpadText.text.ToCharArray().Reverse().ToArray());
                        break;
                    default:
                        break;
                }
            }
            else if (button.LongPressMode)
            {
                numpadText.text += button.LongPressTapAction;
            }
            else
            {
                numpadText.text += button.TapAction;
            }
        }

        private void OnDoubleTap(NumpadButton _)
        {
            foreach (var button in buttons)
            {
                button.DoubleTapMode = !button.DoubleTapMode;
                button.SetActiveMat();
            }
        }

        private void OnLongPress(NumpadButton _)
        {
            foreach (var button in buttons)
            {
                button.LongPressMode = !button.LongPressMode;
                button.SetActiveMat();
            }
        }
    }
}
