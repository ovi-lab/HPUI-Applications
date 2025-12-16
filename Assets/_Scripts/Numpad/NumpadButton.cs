using System.Collections;
using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts
{
    public class NumpadButton : MonoBehaviour
    {
        public UnityEvent<NumpadButton> OnTap;
        public UnityEvent<NumpadButton> OnDoubleTap;
        public UnityEvent<NumpadButton> OnLongPress;

        public bool DoubleTapMode = false;
        public bool LongPressMode = false;

        [SerializeField] private string tapAction;
        [SerializeField] private string doubleTapAction;
        [SerializeField] private string longPressTapAction;

        [SerializeField] private Material baseMat;
        [SerializeField] private Material activeMat;
        [SerializeField] private Material doubleTapBaseMat;
        [SerializeField] private Material doubleTapActiveMat;
        [SerializeField] private Material longPressBaseMat;
        [SerializeField] private Material longPressActiveMat;
        [SerializeField] private Material blankMat;

        public string TapAction => tapAction;
        public string DoubleTapAction => doubleTapAction;
        public string LongPressTapAction => longPressTapAction;

        private HPUIDiscreetGestureDetector gestureDetector;
        private HPUIBaseInteractable baseInteractable;
        private Coroutine setBaseMatRoutine;
        private MeshRenderer meshRenderer;

        private void OnEnable()
        {
            gestureDetector = GetComponent<HPUIDiscreetGestureDetector>();
            baseInteractable = GetComponent<HPUIBaseInteractable>();
            meshRenderer = transform.GetComponentInChildren<MeshRenderer>();

            gestureDetector.OnTap.AddListener(OnTapRecieved);
            gestureDetector.OnDoubleTap.AddListener(OnDoubleTapRecieved);
            gestureDetector.OnLongPress.AddListener(OnLongPressRecieved);

            baseInteractable.GestureEvent.AddListener(OnGestureEvent);
        }

        private void OnDisable()
        {
            gestureDetector.OnTap.RemoveListener(OnTapRecieved);
            baseInteractable.GestureEvent.RemoveListener(OnGestureEvent);
        }

        // private void Update()
        // {
        //     if (DoubleTapMode && string.IsNullOrEmpty(doubleTapAction))
        //     {
        //         gestureDetector.enabled = false;
        //         meshRenderer.material = blankMat;
        //     }
        //     else
        //     {
        //         gestureDetector.enabled = true;
        //         meshRenderer.material = baseMat;
        //     }
        // }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (!string.IsNullOrEmpty(tapAction))
                {
                    baseMat = TryLoadMat(tapAction);
                    activeMat = TryLoadMat($"{tapAction}_active");
                }

                if (!string.IsNullOrEmpty(doubleTapAction))
                {
                    doubleTapBaseMat = TryLoadMat(doubleTapAction);
                    doubleTapActiveMat = TryLoadMat($"{doubleTapAction}_active");
                }

                if (!string.IsNullOrEmpty(longPressTapAction))
                {
                    longPressBaseMat = TryLoadMat(longPressTapAction);
                    longPressActiveMat = TryLoadMat($"{longPressTapAction}_active");
                }
            }
        }

        private Material TryLoadMat(string matName)
        {
            var mat = Resources.Load<Material>($"Numpad/Mats/{matName}");
            if (mat != null)
            {
                return mat;
            }
            return null;
        }

        private void OnTapRecieved(HPUIGestureEventArgs args)
        {
            OnTap?.Invoke(this);
        }

        private void OnDoubleTapRecieved(HPUIGestureEventArgs args)
        {
            OnDoubleTap?.Invoke(this);
        }

        private void OnLongPressRecieved(HPUIGestureEventArgs args)
        {
            OnLongPress?.Invoke(this);
        }

        private void OnGestureEvent(HPUIGestureEventArgs args)
        {
            SetActiveMat();
        }

        public void SetActiveMat()
        {
            // welcome to the spin zone :D
            meshRenderer.material = DoubleTapMode ?
                string.IsNullOrWhiteSpace(doubleTapAction) ?
                blankMat : doubleTapActiveMat :
                LongPressMode ?
                string.IsNullOrWhiteSpace(longPressTapAction) ?
                blankMat :
                longPressActiveMat : activeMat;

            if (setBaseMatRoutine != null) StopCoroutine(setBaseMatRoutine);
            setBaseMatRoutine = StartCoroutine(ResetButtonColor());
        }

        private IEnumerator ResetButtonColor()
        {
            yield return new WaitForSeconds(0.2f);
            meshRenderer.material = DoubleTapMode ?
                string.IsNullOrWhiteSpace(doubleTapAction) ?
                blankMat : doubleTapBaseMat :
                LongPressMode ?
                string.IsNullOrWhiteSpace(longPressTapAction) ?
                blankMat :
                longPressBaseMat : baseMat;
        }
    }
}
