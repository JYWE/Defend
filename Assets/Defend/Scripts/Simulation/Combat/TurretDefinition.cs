using System;

namespace Defend.Simulation
{
    public sealed class TurretDefinition
    {
        public BuildingKind Kind { get; }
        public double Range { get; }
        public int Damage { get; }
        public long EnergyPerShot { get; }
        public int CooldownTicks { get; }
        public double SplashRadius { get; }
        public TurretDefinition(BuildingKind kind, double range, int damage, long energyPerShot,
            int cooldownTicks, double splashRadius = 0)
        {
            if (kind != BuildingKind.Turret && kind != BuildingKind.BurstTurret && kind != BuildingKind.AreaTurret)
                throw new ArgumentException("攻击配置只能绑定炮塔。");
            if (double.IsNaN(range) || double.IsInfinity(range) || range <= 0 || damage <= 0 ||
                energyPerShot <= 0 || cooldownTicks <= 0 || double.IsNaN(splashRadius) ||
                double.IsInfinity(splashRadius) || splashRadius < 0) throw new ArgumentOutOfRangeException(nameof(range));
            Kind = kind; Range = range; Damage = damage; EnergyPerShot = energyPerShot;
            CooldownTicks = cooldownTicks; SplashRadius = splashRadius;
        }
    }
}
