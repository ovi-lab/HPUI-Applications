using ubco.ovilab.ViconUnityStream;
using UnityEngine;

namespace _Scripts.Utils
{
    public class SmoothedFollower : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private Transform lookAtTarget;

        private OneEuroFilter<Vector3> positionFilter;

        private void Start()
        {
            positionFilter = new OneEuroFilter<Vector3>(72);
        }

        private void Update()
        {
            if (lookAtTarget != null) transform.forward = transform.position - lookAtTarget.position;
            if (followTarget != null) transform.position = positionFilter.Filter(followTarget.position);
        }
    }
}
