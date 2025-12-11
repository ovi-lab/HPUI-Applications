using UnityEngine;

namespace _Scripts.Utils
{
    /// <summary>
    /// Provides a collection of extension methods for the <see cref="UnityEngine.Transform"/> class,
    /// simplifying common operations like position manipulation, resetting, and child management.
    /// </summary>
    public static class TransformExtensions
    {
        /// <summary>
        /// Calculates and returns the average world position of all immediate children of a given Transform.
        /// If the parent has no children, its own position is returned.
        /// </summary>
        /// <param name="parent">The parent Transform whose children's average position is to be calculated.</param>
        /// <returns>The average world position of the children, or the parent's position if no children exist.</returns>
        public static Vector3 ReturnAveragePosition(this Transform parent)
        {
            Vector3 averagePosition = Vector3.zero;
            int numberOfChildren = parent.childCount;
            if (numberOfChildren <= 0) return parent.position;
            for (int i = 0; i < numberOfChildren; i++)
            {
                averagePosition += parent.GetChild(i).position;
            }
            averagePosition /= numberOfChildren;
            return averagePosition;
        }

        /// <summary>
        /// Sets the world X-coordinate of the Transform's position while keeping Y and Z coordinates unchanged.
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <param name="x">The new X-coordinate for the world position.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform SetX(this Transform transform, float x)
        {
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
            return transform;
        }

        /// <summary>
        /// Sets the world Y-coordinate of the Transform's position while keeping X and Z coordinates unchanged.
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <param name="y">The new Y-coordinate for the world position.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform SetY(this Transform transform, float y)
        {
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
            return transform;
        }

        /// <summary>
        /// Sets the world Z-coordinate of the Transform's position while keeping X and Y coordinates unchanged.
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <param name="z">The new Z-coordinate for the world position.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform SetZ(this Transform transform, float z)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, z);
            return transform;
        }

        /// <summary>
        /// Sets the local X-coordinate of the Transform's position while keeping local Y and Z coordinates unchanged.
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <param name="x">The new X-coordinate for the local position.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform SetLocalX(this Transform transform, float x)
        {
            transform.localPosition = new Vector3(x, transform.localPosition.y, transform.localPosition.z);
            return transform;
        }

        /// <summary>
        /// Sets the local Y-coordinate of the Transform's position while keeping local X and Z coordinates unchanged.
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <param name="y">The new Y-coordinate for the local position.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform SetLocalY(this Transform transform, float y)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, y, transform.localPosition.z);
            return transform;
        }

        /// <summary>
        /// Sets the local Z-coordinate of the Transform's position while keeping local X and Y coordinates unchanged.
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <param name="z">The new Z-coordinate for the local position.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform SetLocalZ(this Transform transform, float z)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, z);
            return transform;
        }

        /// <summary>
        /// Resets the world position of the Transform to <see cref="Vector3.zero"/>.
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform ResetPosition(this Transform transform)
        {
            transform.position = Vector3.zero;
            return transform;
        }

        /// <summary>
        /// Resets the local position of the Transform to <see cref="Vector3.zero"/>.
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform ResetLocalPosition(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
            return transform;
        }

        /// <summary>
        /// Resets the world rotation of the Transform to <see cref="Quaternion.identity"/> (no rotation).
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform ResetRotation(this Transform transform)
        {
            transform.rotation = Quaternion.identity;
            return transform;
        }

        /// <summary>
        /// Resets the local rotation of the Transform to <see cref="Quaternion.identity"/> (no rotation).
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform ResetLocalRotation(this Transform transform)
        {
            transform.localRotation = Quaternion.identity;
            return transform;
        }

        /// <summary>
        /// Resets the local scale of the Transform to <see cref="Vector3.one"/>.
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform ResetScale(this Transform transform)
        {
            transform.localScale = Vector3.one;
            return transform;
        }

        /// <summary>
        /// Resets the position, rotation, and scale of the Transform.
        /// Position and rotation can be reset either locally or globally based on the <paramref name="isLocal"/> parameter.
        /// Scale is always reset locally.
        /// </summary>
        /// <param name="transform">The Transform to modify.</param>
        /// <param name="isLocal">If true, local position and local rotation are reset; otherwise, world position and world rotation are reset.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform Reset(this Transform transform, bool isLocal = false)
        {
            if (isLocal)
            {
                transform.ResetLocalPosition();
                transform.ResetLocalRotation();
            }
            else
            {
                transform.ResetPosition();
                transform.ResetRotation();
            }
            transform.ResetScale();
            return transform;
        }

        /// <summary>
        /// Destroys all immediate children GameObjects of the Transform.
        /// </summary>
        /// <param name="transform">The parent Transform whose children are to be destroyed.</param>
        /// <param name="recursive">If true, destroys children recursively (i.e., also destroys grandchildren, great-grandchildren, etc.).</param>
        /// <returns>The modified Transform.</returns>
        public static Transform DestroyAllChildren(this Transform transform, bool recursive = false)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (recursive)
                {
                    transform.GetChild(i).DestroyAllChildren(true);
                }
                Object.Destroy(transform.GetChild(i).gameObject);
            }
            return transform;
        }

        /// <summary>
        /// Destroys all immediate children GameObjects of the Transform immediately.
        /// This method should only be used in editor code or during testing, as it can cause issues in runtime builds.
        /// </summary>
        /// <param name="transform">The parent Transform whose children are to be destroyed immediately.</param>
        /// <param name="recursive">If true, destroys children recursively (i.e., also destroys grandchildren, great-grandchildren, etc.) immediately.</param>
        /// <returns>The modified Transform.</returns>
        public static Transform DestroyImmediateAllChildren(this Transform transform, bool recursive = false)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (recursive)
                {
                    transform.GetChild(i).DestroyImmediateAllChildren(true);
                }
                Object.DestroyImmediate(transform.GetChild(i).gameObject);
            }
            return transform;
        }
    }
}
