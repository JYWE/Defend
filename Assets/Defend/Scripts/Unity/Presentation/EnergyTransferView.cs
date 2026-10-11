using Defend.Simulation;
using UnityEngine;

namespace Defend.Unity
{
    /// <summary>能量白盒只插值表现已存在的运输记录；隐藏或回收它不会改变能量。</summary>
    public sealed class EnergyTransferView : MonoBehaviour
    {
        public void Apply(EnergyTransferSnapshot transfer, double displayTick)
        {
            double duration = System.Math.Max(1, transfer.ArrivalTick - transfer.StartTick);
            float progress = Mathf.Clamp01((float)((displayTick - transfer.StartTick) / duration));
            Vector3 from = new Vector3((float)transfer.SourceX, 0.85f, (float)transfer.SourceZ);
            Vector3 to = new Vector3((float)transfer.TargetX, 0.85f, (float)transfer.TargetZ);
            transform.position = Vector3.Lerp(from, to, progress) + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.25f);
        }
    }
}
