using EditorAttributes;
using UnityEngine;

namespace _Scripts.Utils
{
    public class MakeChild : MonoBehaviour
    {
        [Tooltip("The target parent to make this object a child of")]
        [SerializeField] private Transform targetParent;

        /// <summary>
        /// The target parent to make this object a child of
        /// </summary>
        public Transform TargetParent
        {
            get => targetParent;
            set => targetParent = value;
        }

        [Tooltip("Sets object as a child of the target on start")]
        [SerializeField] private bool adoptOnStart = true;

        /// <summary>
        /// Sets object as a child of the target on start
        /// </summary>
        public bool AdoptOnStart
        {
            get => adoptOnStart;
            set => adoptOnStart = value;
        }

        [Header("Offset Settings")]

        [Tooltip("The local position offset to apply when making this object a child")]
        [SerializeField] private Vector3 localPositionOffset = Vector3.zero;

        /// <summary>
        /// The local position offset to apply when making this object a child
        /// </summary>
        public Vector3 LocalPositionOffset
        {
            get => localPositionOffset;
            set => localPositionOffset = value;
        }

        [Tooltip("The local rotation offset to apply when making this object a child")]
        [SerializeField] private Vector3 localRotationOffset = Vector3.zero;

        /// <summary>
        /// The local rotation offset to apply when making this object a child
        /// </summary>
        public Vector3 LocalRotationOffset
        {
            get => localRotationOffset;
            set => localRotationOffset = value;
        }

        void Start()
        {
            if (adoptOnStart && targetParent != null)
            {
                Child();
            }
        }

        [Button]
        public void Child()
        {
            transform.SetParent(targetParent, false);
            transform.localPosition = localPositionOffset;
            transform.localRotation = Quaternion.Euler(localRotationOffset);
        }
    }
}
