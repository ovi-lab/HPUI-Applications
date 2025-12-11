using UnityEngine;
using Unity.Burst;

namespace _Scripts.Utils
{
    /// <summary>
    /// Vector Extensions to assist with quick Vector2 and Vector3 Manipulations
    /// </summary>
    [BurstCompile]
    public static class VectorExtensions
    {
        /// <summary>
        /// Projects a Vector3 onto the XZ plane, optionally setting the Y component.
        /// </summary>
        /// <param name="vec">The original Vector3.</param>
        /// <param name="y">The desired Y component. Defaults to 0.</param>
        /// <returns>A new Vector3 with the X and Z components from the original vector and the specified Y component.</returns>
        public static Vector3 XZPlane(this Vector3 vec, float y = 0) => new Vector3(vec.x, y, vec.z);

        /// <summary>
        /// Projects a Vector3 onto the YZ plane (sets X to 0).
        /// </summary>
        /// <param name="vec">The original Vector3.</param>
        /// <returns>A new Vector3 with the Y and Z components from the original vector and X set to 0.</returns>
        public static Vector3 YZPlane(this Vector3 vec) => new Vector3(0, vec.y, vec.z);

        /// <summary>
        /// Projects a Vector3 onto the XY plane (sets Z to 0).
        /// </summary>
        /// <param name="vec">The original Vector3.</param>
        /// <returns>A new Vector3 with the X and Y components from the original vector and Z set to 0.</returns>
        public static Vector3 XYPlane(this Vector3 vec) => new Vector3(vec.x, vec.y, 0);

        /// <summary>
        /// Sets the X component of a Vector3.
        /// </summary>
        /// <param name="vec">The original Vector3.</param>
        /// <param name="x">The new X component.</param>
        /// <returns>A new Vector3 with the updated X component.</returns>
        public static Vector3 SetX(this Vector3 vec, float x) => new Vector3(x, vec.y, vec.z);

        /// <summary>
        /// Sets the Y component of a Vector3.
        /// </summary>
        /// <param name="vec">The original Vector3.</param>
        /// <param name="y">The new Y component.</param>
        /// <returns>A new Vector3 with the updated Y component.</returns>
        public static Vector3 SetY(this Vector3 vec, float y) => new Vector3(vec.x, y, vec.z);

        /// <summary>
        /// Sets the Z component of a Vector3.
        /// </summary>
        /// <param name="vec">The original Vector3.</param>
        /// <param name="z">The new Z component.</param>
        /// <returns>A new Vector3 with the updated Z component.</returns>
        public static Vector3 SetZ(this Vector3 vec, float z) => new Vector3(vec.x, vec.y, z);

        /// <summary>
        /// Calculates the biased Euclidean distance between two Vector3 points.
        /// </summary>
        /// <param name="firstVector">The first Vector3.</param>
        /// <param name="secondVector">The second Vector3.</param>
        /// <param name="wx">Weight for the X component difference. Defaults to 1f.</param>
        /// <param name="wy">Weight for the Y component difference. Defaults to 1f.</param>
        /// <param name="wz">Weight for the Z component difference. Defaults to 1f.</param>
        /// <returns>The biased distance between the two vectors.</returns>
        public static float BiasedDistance(Vector3 firstVector, Vector3 secondVector, float wx = 1f, float wy = 1f, float wz = 1f)
        {
            float dx = (firstVector.x - secondVector.x) * wx;
            float dy = (firstVector.y - secondVector.y) * wy;
            float dz = (firstVector.z - secondVector.z) * wz;

            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Calculates the biased Euclidean distance between two Vector2 points.
        /// </summary>
        /// <param name="firstVector">The first Vector2.</param>
        /// <param name="secondVector">The second Vector2.</param>
        /// <param name="wx">Weight for the X component difference. Defaults to 1f.</param>
        /// <param name="wy">Weight for the Y component difference. Defaults to 1f.</param>
        /// <returns>The biased distance between the two vectors.</returns>
        public static float BiasedDistance(Vector2 firstVector, Vector2 secondVector, float wx = 1f, float wy = 1f)
        {
            float dx = (firstVector.x - secondVector.x) * wx;
            float dy = (firstVector.y - secondVector.y) * wy;

            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Calculates the squared biased Euclidean distance between two 3D points given their components.
        /// This avoids a square root operation, which can be faster for comparisons.
        /// </summary>
        /// <param name="firstX">X component of the first point.</param>
        /// <param name="firstY">Y component of the first point.</param>
        /// <param name="firstZ">Z component of the first point.</param>
        /// <param name="secondX">X component of the second point.</param>
        /// <param name="secondY">Y component of the second point.</param>
        /// <param name="secondZ">Z component of the second point.</param>
        /// <param name="wx">Weight for the X component difference. Defaults to 1f.</param>
        /// <param name="wy">Weight for the Y component difference. Defaults to 1f.</param>
        /// <param name="wz">Weight for the Z component difference. Defaults to 1f.</param>
        /// <returns>The squared biased distance between the two points.</returns>
        [BurstCompile]
        public static float BiasedDistanceSqr(float firstX, float firstY, float firstZ, float secondX, float secondY, float secondZ, float wx = 1f, float wy = 1f, float wz = 1f)
        {
            float dx = (firstX - secondX) * wx;
            float dy = (firstY - secondY) * wy;
            float dz = (firstZ - secondZ) * wz;

            return dx * dx + dy * dy + dz * dz;
        }
    }
}
