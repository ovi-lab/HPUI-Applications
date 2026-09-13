using UnityEngine;

namespace _Scripts.Utils
{
    public class ToggleMeshRenderer : MonoBehaviour
    {
        [SerializeField] private MeshRenderer mr;

        public void ToggleRenderer()
        {
            mr.enabled = !mr.enabled;
        }
    }
}
