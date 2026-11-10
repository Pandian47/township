using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Gentle up-and-down float for badges above buildings ("ready" product, construction hammer, padlock).
    /// Owners set the rest position with <see cref="Place"/> instead of writing localPosition directly.
    /// </summary>
    public sealed class FloatBob : MonoBehaviour
    {
        [SerializeField] private float amplitude = 0.06f;
        [SerializeField] private float speed = 2.4f;

        private Vector3 _base;
        private float _phase;

        public Vector3 BaseLocalPosition => _base;

        /// <summary>Sets the badge's rest position (and adds the bob if it isn't there yet).</summary>
        public static FloatBob Place(Transform badge, Vector3 localPosition)
        {
            var bob = badge.GetComponent<FloatBob>();
            if (bob == null)
            {
                bob = badge.gameObject.AddComponent<FloatBob>();
                bob._phase = Random.value * Mathf.PI * 2f;
            }

            bob._base = localPosition;
            badge.localPosition = localPosition;
            return bob;
        }

        private void LateUpdate()
        {
            transform.localPosition = _base + new Vector3(0f, Mathf.Sin(Time.unscaledTime * speed + _phase) * amplitude, 0f);
        }
    }
}
