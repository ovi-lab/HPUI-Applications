using System;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;

namespace _Scripts.Utils
{
    public class MakeChild : MonoBehaviour
    {
        [Tooltip("Mapping for the target parents and children")]
        [SerializeField] private List<ParentChildPair> parentChildrenPairs;

        /// <summary>
        /// Mapping for the target parents and children
        /// </summary>
        public List<ParentChildPair> ParentChildPairs
        {
            get => parentChildrenPairs;
            set => parentChildrenPairs = value;
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

        void Start()
        {
            if (adoptOnStart && parentChildrenPairs != null && parentChildrenPairs.Count > 0)
            {
                Child();
            }
        }

        [Button]
        public void Child()
        {
            foreach (var parentChildPair in parentChildrenPairs)
            {
                parentChildPair.Child.SetParent(parentChildPair.Parent, false);
                parentChildPair.Child.localPosition = parentChildPair.PositionOffset;
                parentChildPair.Child.localRotation = Quaternion.identity;
                // why? why do I ever?
                parentChildPair.Child.Rotate(90f, 0f, 0f, Space.Self);
            }
        }
    }

    [Serializable]
    public class ParentChildPair
    {
        public Transform Parent;
        public Transform Child;
        public Vector3 PositionOffset;
    }
}
