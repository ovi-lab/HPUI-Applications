using UnityEngine;

namespace _Scripts.Utils
{
    /// <summary>
    /// A base class for creating Singleton MonoBehaviors in Unity.
    /// This ensures that only one instance of the class exists throughout the application lifetime.
    /// The singleton instance is accessible via the static `Instance` property.
    /// </summary>
    /// <typeparam name="T">The type of the MonoBehaviour that will be a Singleton.</typeparam>
    public abstract class SingletonMonoBehavior<T> : MonoBehaviour where T : MonoBehaviour, new()
    {
        private static T _instance;

        /// <summary>
        /// Gets the singleton instance of this MonoBehaviour.
        /// If an instance does not exist, it will be created and assigned.
        /// </summary>
        public static T Instance => _instance;

        /// <summary>
        /// Called when the script instance is being loaded.
        /// It ensures that only one instance of the singleton exists. If another instance is found, it destroys the new GameObject.
        /// </summary>
        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = GetComponent<T>();
        }
    }
}
