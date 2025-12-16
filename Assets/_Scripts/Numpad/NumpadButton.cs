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
        public bool DoubleTapMode = false;

        [SerializeField] private string tapAction;
        [SerializeField] private string doubleTapAction;
        [SerializeField] private string longPressTapAction;

        [SerializeField] private Material baseMat;
        [SerializeField] private Material activeMat;
        [SerializeField] private Material doubleTapBaseMat;
        [SerializeField] private Material doubleTapActiveMat;
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
                    TryLoadMat(tapAction, ref baseMat);
                    TryLoadMat($"{tapAction}_active", ref activeMat);
                }

                if (!string.IsNullOrEmpty(doubleTapAction))
                {
                    TryLoadMat(doubleTapAction, ref doubleTapBaseMat);
                    TryLoadMat($"{doubleTapAction}_active", ref doubleTapActiveMat);
                }
            }
        }

        private void TryLoadMat(string matName, ref Material target)
        {
            var mat = Resources.Load<Material>($"Numpad/Mats/{matName}");
            if (mat != null)
            {
                target = mat;
            }
        }

        private void OnTapRecieved(HPUIGestureEventArgs args)
        {
            OnTap?.Invoke(this);
        }

        private void OnDoubleTapRecieved(HPUIGestureEventArgs args)
        {
            OnDoubleTap?.Invoke(this);
        }

        private void OnGestureEvent(HPUIGestureEventArgs args)
        {
            SetActiveMat();
        }

        public void SetActiveMat()
        {
            meshRenderer.material = DoubleTapMode ? string.IsNullOrWhiteSpace(doubleTapAction) ? blankMat : doubleTapActiveMat : activeMat;

            if (setBaseMatRoutine != null) StopCoroutine(setBaseMatRoutine);
            setBaseMatRoutine = StartCoroutine(ResetButtonColor());
        }

        private IEnumerator ResetButtonColor()
        {
            yield return new WaitForSeconds(0.2f);
            meshRenderer.material = DoubleTapMode ? string.IsNullOrWhiteSpace(doubleTapAction) ? blankMat : doubleTapBaseMat : baseMat;
        }
    }
}
