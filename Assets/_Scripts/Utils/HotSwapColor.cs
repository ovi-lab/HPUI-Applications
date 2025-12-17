using UnityEngine;

namespace _Scripts.Utils
{
    /// <summary>
    /// Component to allow hot-swapping of colors on a MeshRenderer's material at runtime or in editor.
    /// It uses a MaterialPropertyBlock to avoid creating new material instances.
    /// </summary>
    public class HotSwapColor : MonoBehaviour
    {
        /// <summary>
        /// The base color to apply to the material.
        /// </summary>
        [SerializeField] private Color color;

        /// <summary>
        /// The MeshRenderer component to apply the color to.
        /// </summary>
        [SerializeField] private MeshRenderer mr;

        /// <summary>
        /// The shader property ID for the main color.
        /// </summary>
        private static readonly int ShaderProp = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock mpb;

        /// <summary>
        /// Gets the MaterialPropertyBlock, creating it if it doesn't already exist.
        /// </summary>
        private MaterialPropertyBlock Mpb => mpb ??= new MaterialPropertyBlock();

        /// <summary>
        /// Called when the object becomes enabled and active. Applies the current color.
        /// </summary>
        private void OnEnable()
        {
            ApplyColor();
        }

        /// <summary>
        /// Called in the editor when the script is loaded or a value is changed in the inspector.
        /// Applies the current color for immediate visual feedback.
        /// </summary>
        private void OnValidate()
        {
            FetchMr();
            ApplyColor();
        }

        /// <summary>
        /// Sets a new color for the material and applies it.
        /// </summary>
        /// <param name="color">The new color to set.</param>
        public void SetColor(Color color)
        {
            this.color = color;
            ApplyColor();
        }

        /// <summary>
        /// Fetches the MeshRenderer component attached to this GameObject.
        /// </summary>
        public void FetchMr()
        {
            if (mr == null)
            {
                mr = GetComponent<MeshRenderer>();
            }
        }

        /// <summary>
        /// Applies the currently set color to the MeshRenderer using a MaterialPropertyBlock.
        /// </summary>
        private void ApplyColor()
        {
            Mpb.SetColor(ShaderProp, color);
            if (mr != null)
            {
                mr.SetPropertyBlock(Mpb);
            }
        }
    }
}
