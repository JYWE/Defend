using System;
using UnityEngine;

namespace Defend.PhaseFiveEffects
{
    [CreateAssetMenu(menuName = "Defend/Phase Five Effect Library")]
    public sealed class EffectLibrary : ScriptableObject
    {
        [Serializable] public struct Entry { public string Id; public PhaseFiveEffect Prefab; }
        public Entry[] Entries = Array.Empty<Entry>();
    }
}
