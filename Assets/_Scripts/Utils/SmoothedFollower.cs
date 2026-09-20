using ubco.ovilab.ViconUnityStream;
using UnityEngine;

namespace _Scripts.Utils
{
    public class SmoothedFollower : MonoBehaviour
    {
        [SerializeField]
        private Transform followTarget;

        [SerializeField]
        private Transform lookAtTarget;

        private OneEuroFilter<Vector3> positionFilter;

        /// <summary>
        /// If true, the transform's forward is hard-set each frame to the follow target's
        /// local up direction (world space), using the target's forward as the transform's up.
        /// </summary>
        [SerializeField]
        private bool orientFromTargetUp;

        /// <summary>
        /// Extra rotation (euler degrees) applied after orientFromTargetUp, e.g. (90, 0, 0)
        /// for a 90 degree X rotation.
        /// </summary>
        [SerializeField]
        private Vector3 rotationOffset;

        /// <summary>
        /// Transform this follower smoothly tracks.
        /// </summary>
        public Transform FollowTarget
        {
            get => followTarget;
            set => followTarget = value;
        }

        /// <summary>
        /// Whether the transform's forward follows the follow target's local up direction.
        /// </summary>
        public bool OrientFromTargetUp
        {
            get => orientFromTargetUp;
            set => orientFromTargetUp = value;
        }

        /// <summary>
        /// Extra rotation (euler degrees) applied after orientFromTargetUp.
        /// </summary>
        public Vector3 RotationOffset
        {
            get => rotationOffset;
            set => rotationOffset = value;
        }

        private void Start()
        {
            positionFilter = new OneEuroFilter<Vector3>(72);
        }

        private void Update()
        {
            if (followTarget != null)
            {
                if (orientFromTargetUp)
                {
                    transform.rotation = Quaternion.LookRotation(followTarget.TransformDirection(Vector3.up), -followTarget.TransformDirection(Vector3.forward)) * Quaternion.Euler(rotationOffset);
                }
                transform.position = positionFilter.Filter(followTarget.position);
            }
            if (lookAtTarget != null)
                transform.forward = transform.position - lookAtTarget.position;
        }
    }
}
