using UnityEngine;

namespace _Scripts.ColorPicker
{
    public class RandomRotate : MonoBehaviour
    {
        [Header("Speed Settings")]
        [SerializeField] private float minSpeed = 10f;
        [SerializeField] private float maxSpeed = 60f;

        [Header("Change Timing")]
        [SerializeField] private float minChangeInterval = 1.5f;
        [SerializeField] private float maxChangeInterval = 4f;

        [Header("Smoothing")]
        [SerializeField] private float speedLerpRate = 1.5f;
        [SerializeField] private float axisLerpRate = 2f;

        private Vector3 currentAxis;
        private Vector3 targetAxis;

        private float currentSpeed;
        private float targetSpeed;
        private float timer;

        private void Start()
        {
            PickNewRotation();
            currentAxis = targetAxis;
            currentSpeed = targetSpeed;
        }

        private void Update()
        {
            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                PickNewRotation();
            }

            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * speedLerpRate);
            currentAxis = Vector3.Slerp(currentAxis, targetAxis, Time.deltaTime * axisLerpRate);

            transform.Rotate(currentAxis, currentSpeed * Time.deltaTime, Space.World);
        }

        private void PickNewRotation()
        {
            targetAxis = Random.onUnitSphere;
            targetSpeed = Random.Range(minSpeed, maxSpeed);
            timer = Random.Range(minChangeInterval, maxChangeInterval);
        }
    }
}
