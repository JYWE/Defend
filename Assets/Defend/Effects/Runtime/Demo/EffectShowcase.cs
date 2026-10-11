using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Defend.PhaseFiveEffects
{
    /// <summary>Independent art demonstration. It has no reference to the combat simulation.</summary>
    public sealed class EffectShowcase : MonoBehaviour
    {
        public EffectPool Pool;
        public Camera OverviewCamera, CloseCamera;
        public Transform[] Subjects, Targets;
        public Transform[] Muzzles;
        public Transform[] SubjectAnchors, TargetAnchors, TargetPorts;
        public bool Playing = true;
        public bool ExternalClock;
        public string SingleEffect = "";
        public float CycleTime { get; private set; }
        public int Cycles { get; private set; }
        long sequence;
        int nextEvent;
        const float Duration = 3.2f;
        static readonly float[] EventTimes = { 0f, .16f, .32f, .48f, .80f, 1.10f, 1.42f, 1.8f, 2.2f, 2.5f };

        void Start() { Restart(); }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
                if (keyboard.rKey.wasPressedThisFrame) Restart();
                if (keyboard.sKey.wasPressedThisFrame) Stop();
                if (keyboard.digit1Key.wasPressedThisFrame) SetCloseView(false);
                if (keyboard.digit2Key.wasPressedThisFrame) SetCloseView(true);
                if (keyboard.tabKey.wasPressedThisFrame) NextSingle();
                if (keyboard.enterKey.wasPressedThisFrame) { SingleEffect = ""; Restart(); }
            }
            if (!ExternalClock) AdvanceDemo(Time.deltaTime);
        }
        public void SetCloseView(bool close) { OverviewCamera.enabled = !close; CloseCamera.enabled = close; }
        public void TogglePause() { Playing = !Playing; Pool.Paused = !Playing; }
        public void Stop() { Playing = false; Pool.Paused = false; Pool.StopAll(); }
        public void Restart()
        {
            Pool.AutomaticAdvance = false; Pool.Paused = false; Pool.StopAll();
            CycleTime = 0; nextEvent = 0; Cycles = 0; Playing = true;
            if (string.IsNullOrEmpty(SingleEffect)) StartStatuses();
        }
        void NextSingle()
        {
            int index = -1;
            for (int i = 0; i < Pool.Library.Entries.Length; i++) if (Pool.Library.Entries[i].Id == SingleEffect) index = i;
            SingleEffect = Pool.Library.Entries[(index + 1) % Pool.Library.Entries.Length].Id;
            Restart();
        }
        public void AdvanceDemo(float deltaTime)
        {
            if (!Playing || deltaTime <= 0) return;
            Pool.Advance(deltaTime);
            CycleTime += deltaTime;
            while (nextEvent < EventTimes.Length && EventTimes[nextEvent] <= CycleTime)
            {
                FireEvent(nextEvent); nextEvent++;
            }
            if (CycleTime >= Duration)
            {
                CycleTime %= Duration; nextEvent = 0; Cycles++;
                if (!string.IsNullOrEmpty(SingleEffect)) Pool.StopAll();
            }
        }
        public EffectRequest RequestFor(int station, Vector3 origin, Vector3 destination)
        {
            Vector3 direction = destination - origin;
            return new EffectRequest { Origin = origin, Destination = destination, Subject = Subjects[station],
                Rotation = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : Quaternion.identity,
                Intensity = 1, Seed = 17 + station, EventSequence = ++sequence, ConfirmedTransfer = true };
        }
        void Emit(string id, int station, bool onTarget = false)
        {
            Transform subject = onTarget ? Targets[station] : Subjects[station];
            Vector3 origin = subject.position + Vector3.up * .28f;
            if (id.Contains("Impact") || id == "HeavyContact") origin.y = .13f;
            if (id == "EnemyDeath" || id == "BuildingDestruction" || id == "RepairComplete") origin = subject.position;
            if (id == "HeavyContact") origin = subject.position + subject.forward * .55f + Vector3.up * .18f;
            if (id == "BuildingHit") origin += Vector3.back * .32f;
            if (id.Contains("Running") || id.Contains("Energy") || id == "HeavyLock" || id == "SearchMarker" || id.Contains("Shield") || id == "PortSend")
                origin = (onTarget ? TargetAnchors[station] : SubjectAnchors[station]).position;
            if (id == "PortSend" || id == "EnergyArrival") origin = (onTarget ? TargetPorts[station] : Muzzles[station]).position;
            var request = RequestFor(station, origin, origin);
            request.Subject = subject; request.Rotation = Quaternion.identity;
            Pool.TryPlay(id, request);
        }
        void Shot(int station, string muzzle, string tracer, string hit)
        {
            Vector3 origin = Muzzles[station].position;
            Vector3 target = Targets[station].position + Vector3.up * .22f;
            // Aim at the visible upper armour; short showcase distances otherwise exaggerate vertical mismatch.
            if (station == 1) target.y = Targets[station].position.y + .46f;
            var request = RequestFor(station, origin, target);
            Pool.TryPlay(muzzle, request);
            // Both line and impact are spawned in this call: there is no visual flight delay.
            if (!string.IsNullOrEmpty(tracer)) Pool.TryPlay(tracer, request);
            Pool.TryPlay(hit, request);
            Emit("EnemyHit", station, true);
        }
        void StartStatuses()
        {
            Emit("ReactorRunning", 6);
            Emit("RelayRunning", 6, true);
            Emit("EnergyShortage", 7);
            Emit("EnergyDisconnected", 7, true);
            Emit("HeavyLock", 1, true);
            Emit("SearchMarker", 2);
        }
        void FireEvent(int index)
        {
            if (!string.IsNullOrEmpty(SingleEffect))
            {
                if (index == 0)
                {
                    string id = SingleEffect;
                    int station = id.StartsWith("Heavy") ? 1 : id.StartsWith("Area") ? 2 : id.StartsWith("Enemy") ? 3 : id.Contains("Building") ? 4 : id.Contains("Repair") ? 5 : id.Contains("Shield") ? 8 : id.Contains("Energy") || id.Contains("Running") || id.Contains("Port") ? 6 : 0;
                    var request = RequestFor(station, Muzzles[station].position, Targets[station].position + Vector3.up * .2f);
                    if (id.Contains("Death") || id.Contains("Destruction") || id.Contains("Hit") || id.Contains("Repair") || id.Contains("Shield")) request.Origin = request.Destination = Subjects[station].position + Vector3.up * .25f;
                    Pool.TryPlay(id, request);
                }
                return;
            }
            switch (index)
            {
                case 0: Shot(0, "RapidMuzzle", "RapidTracer", "RapidImpact"); Emit("PortSend", 6); Pool.TryPlay("EnergyTransfer", RequestFor(6, Muzzles[6].position, TargetPorts[6].position)); break;
                case 1: Shot(0, "RapidMuzzle", "RapidTracer", "RapidImpact"); break;
                case 2: Shot(0, "RapidMuzzle", "RapidTracer", "RapidImpact"); Shot(1, "HeavyMuzzle", "HeavyTracer", "HeavyImpact"); break;
                case 3: Emit("EnergyArrival", 6, true); break;
                case 4: Shot(2, "AreaDischarge", "", "AreaImpact"); Emit("EnemyHit", 3); Emit("BuildingHit", 4); break;
                case 5: Emit("HeavyContact", 1, true); Emit("ShieldHit", 8); break;
                case 6: Emit("EnemyDeath", 3); Emit("BuildingDestruction", 4); break;
                case 7: Emit("RepairComplete", 5); break;
                case 8: Emit("ShieldBreak", 8); break;
                case 9: Shot(0, "RapidMuzzle", "RapidTracer", "RapidImpact"); break;
            }
        }
        void OnDisable() { if (Pool != null) Pool.StopAll(); }
    }
}
