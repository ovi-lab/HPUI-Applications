using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Keyboard.Text.DebugData
{
    public class KeyboardRawInputDisplay : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI tmp;

        [SerializeField]
        private Image panel;

        [SerializeField]
        private FingerRowCapture capture;

        private bool active = false;

        private void OnEnable()
        {
            capture.OnRawPositionDebug.AddListener(UpdateTmp);
        }

        private void OnDisable()
        {
            capture.OnRawPositionDebug.RemoveListener(UpdateTmp);
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
