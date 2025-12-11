using System;
using System.Collections.Generic;

namespace _Scripts.Utils
{
    /// <summary>
    /// Provides extension methods for various list operations.
    /// </summary>
    public static class ListExtensions
    {
        private static Random rng;

        /// <summary>
        /// Shuffles the elements of an <see cref="IList{T}"/> using the Fisher-Yates algorithm.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to be shuffled.</param>
        /// <param name="seed">The seed for the random number generator, ensuring reproducible shuffles if desired.</param>
        public static void Shuffle<T>(this IList<T> list, int seed)
        {
            rng = new Random(seed);
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}
