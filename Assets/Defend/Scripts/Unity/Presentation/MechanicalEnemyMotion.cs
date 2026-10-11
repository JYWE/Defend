using UnityEngine;

namespace Defend.Unity
{
    /// <summary>步态取决于表现位移，暂停或原地攻击时不会继续滑步。</summary>
    public sealed class MechanicalEnemyMotion : MonoBehaviour
    {
        public Transform[] Legs;
        private Quaternion[] rest;
        private Vector3 previous;
        private float distance;
        private void OnEnable()
        {
            if (rest == null) { rest = new Quaternion[Legs.Length]; for (int i = 0; i < Legs.Length; i++) rest[i] = Legs[i].localRotation; }
            previous = transform.position; distance = 0;
            for (int i = 0; i < Legs.Length; i++) Legs[i].localRotation = rest[i];
        }
        public void ApplyPosition()
        {
            float moved = Vector3.Distance(transform.position, previous);
            previous = transform.position;
            if (moved > 2 || moved < .00001f) return;
            distance += moved;
            for (int i = 0; i < Legs.Length; i++)
                Legs[i].localRotation = rest[i] * Quaternion.Euler(Mathf.Sin(distance * 11 + (i == 0 || i == 3 ? 0 : Mathf.PI)) * 14, 0, 0);
        }
    }
}
