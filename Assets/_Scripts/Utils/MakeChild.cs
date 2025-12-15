using System;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;

namespace _Scripts.Utils
{
    /// <summary>
    /// Mainly using this script as a substitute for <see cref="ubco.ovilab.HPUI.Core.Tracking.JointFollower"/>
    /// The issue there is it has this lagginess to the following, which we don't want for demos
    /// This means more of a headache setting up for better visuals
    /// </summary>
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

        /// <summary>
        /// Called when the script instance is being loaded.
        /// If <see cref="adoptOnStart"/> is true, it calls the <see cref="Child"/> method to set up parent-child relationships.
        /// </summary>
        void Start()
        {
            if (adoptOnStart && parentChildrenPairs != null && parentChildrenPairs.Count > 0)
            {
                Child();
            }
        }

        /// <summary>
        /// Sets each child object to be a child of its respective parent,
        /// applies the defined position offset, and sets local rotation.
        /// </summary>
        [Button]
        public void Child()
        {
            foreach (var parentChildPair in parentChildrenPairs)
            {
                parentChildPair.Child.SetParent(parentChildPair.Parent, false);
                parentChildPair.Child.localPosition = parentChildPair.PositionOffset;
                parentChildPair.Child.localRotation = Quaternion.identity;
                parentChildPair.Child.Rotate(90f, 0f, 0f, Space.Self);
            }
        }

        /// <summary>
        /// Reverts each child object to be a child of this GameObject's transform,
        /// resetting their local position, rotation, and scale.
        /// </summary>
        [Button]
        public void Revert()
        {
            foreach (var parentChildPair in parentChildrenPairs)
            {
                parentChildPair.Child.SetParent(this.transform);
                parentChildPair.Child.transform.position = Vector3.zero;
                parentChildPair.Child.transform.rotation = Quaternion.Euler(Vector3.zero);
                parentChildPair.Child.transform.localScale = Vector3.one * 0.006f; // TODO: I will unhard code this someday :D
                parentChildPair.Name = parentChildPair.Child.name;
            }
        }
    }

    /// <summary>
    /// Represents a pair of Transform objects, defining a parent and a child, along with a position offset.
    /// </summary>
    [Serializable]
    public class ParentChildPair
    {
        [Tooltip("Name")]
        public string Name;
        [Tooltip("The parent Transform.")]
        public Transform Parent;
        [Tooltip("The child Transform.")]
        public Transform Child;
        [Tooltip("The local position offset for the child relative to the parent.")]
        public Vector3 PositionOffset;
    }
}
