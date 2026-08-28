using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// An ordered list of waypoints, authored by dropping an empty GameObject per
    /// point and parenting them under this one.
    /// </summary>
    public sealed class PatrolRoute : MonoBehaviour
    {
        [Tooltip("Walk the route forwards then backwards instead of looping to the start.")]
        [SerializeField] private bool pingPong;

        [Tooltip("Seconds to wait at each waypoint. A guard that never pauses is trivial to time.")]
        [SerializeField, Min(0f)] private float waitAtWaypoint = 2.5f;

        private Transform[] _points;

        public float WaitSeconds => waitAtWaypoint;
        public int Count => Points.Length;

        private Transform[] Points
        {
            get
            {
                if (_points != null) return _points;

                _points = new Transform[transform.childCount];
                for (int i = 0; i < transform.childCount; i++) _points[i] = transform.GetChild(i);
                return _points;
            }
        }

        public Vector3 PositionAt(int index) =>
            Count == 0 ? transform.position : Points[Mathf.Clamp(index, 0, Count - 1)].position;

        /// <summary>Advances an index along the route, honouring the ping-pong flag.</summary>
        public int NextIndex(int current, ref int direction)
        {
            if (Count <= 1) return 0;

            if (!pingPong) return (current + 1) % Count;

            int next = current + direction;
            if (next >= Count || next < 0)
            {
                direction = -direction;
                next = current + direction;
            }
            return Mathf.Clamp(next, 0, Count - 1);
        }

        private void OnDrawGizmos()
        {
            if (Count == 0) return;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            for (int i = 0; i < Count; i++)
            {
                Vector3 point = PositionAt(i);
                Gizmos.DrawWireSphere(point, 0.35f);

                bool hasNext = i < Count - 1;
                if (hasNext) Gizmos.DrawLine(point, PositionAt(i + 1));
                else if (!pingPong && Count > 2) Gizmos.DrawLine(point, PositionAt(0));
            }
        }
    }
}
