using System;
using Defend.Simulation;

namespace Defend.Unity
{
    /// <summary>仅由明确启用的验收调用；经过正常命令入口购买和维修，不修改权威状态。</summary>
    public static class PhaseThreeAcceptancePlan
    {
    public static void Deploy(GameWorld world, int strategy, int wave, Action<WorldCommand> submit)
    {
        if (strategy == 0)
        {
            if (wave == 1) { submit(WorldCommand.Place(BuildingKind.AreaTurret, 22, 18)); submit(WorldCommand.Place(BuildingKind.BurstTurret, 25, 21)); submit(WorldCommand.Place(BuildingKind.Reactor, 29, 24)); }
            if (wave == 2) submit(WorldCommand.Place(BuildingKind.AreaTurret, 22, 23));
            if (wave == 3) { submit(WorldCommand.Place(BuildingKind.BurstTurret, 23, 19)); submit(WorldCommand.Place(BuildingKind.Relay, 23, 21)); }
            if (wave == 4) { submit(WorldCommand.Place(BuildingKind.Turret, 21, 24)); submit(WorldCommand.Place(BuildingKind.Reactor, 21, 21)); }
            if (wave == 5) { submit(WorldCommand.Place(BuildingKind.BurstTurret, 23, 23)); submit(WorldCommand.Place(BuildingKind.Relay, 25, 25)); }
            if (wave == 6) { submit(WorldCommand.Place(BuildingKind.AreaTurret, 20, 18)); submit(WorldCommand.Place(BuildingKind.Turret, 21, 17)); }
            if (wave == 7) { submit(WorldCommand.Place(BuildingKind.BurstTurret, 21, 26)); submit(WorldCommand.Place(BuildingKind.Reactor, 22, 26)); }
            if (wave == 8) { submit(WorldCommand.Place(BuildingKind.AreaTurret, 20, 24)); submit(WorldCommand.Place(BuildingKind.Turret, 22, 16)); }
        }
        else
        {
            if (wave == 1) { submit(WorldCommand.Place(BuildingKind.Reactor, 17, 12)); submit(WorldCommand.Place(BuildingKind.AreaTurret, 16, 10)); submit(WorldCommand.Place(BuildingKind.BurstTurret, 18, 13)); }
            if (wave == 2) { submit(WorldCommand.Place(BuildingKind.Reactor, 17, 28)); submit(WorldCommand.Place(BuildingKind.Turret, 17, 30)); }
            if (wave == 3) submit(WorldCommand.Place(BuildingKind.BurstTurret, 16, 13));
            if (wave == 4) { submit(WorldCommand.Place(BuildingKind.AreaTurret, 16, 29)); submit(WorldCommand.Place(BuildingKind.Relay, 21, 26)); }
            if (wave == 5) { submit(WorldCommand.Place(BuildingKind.BurstTurret, 18, 27)); submit(WorldCommand.Place(BuildingKind.Relay, 21, 14)); }
            if (wave == 6) { submit(WorldCommand.Place(BuildingKind.AreaTurret, 20, 12)); submit(WorldCommand.Place(BuildingKind.Reactor, 21, 19)); }
            if (wave == 7) { submit(WorldCommand.Place(BuildingKind.Turret, 18, 10)); submit(WorldCommand.Place(BuildingKind.BurstTurret, 20, 28)); }
            if (wave == 8) { submit(WorldCommand.Place(BuildingKind.AreaTurret, 20, 26)); submit(WorldCommand.Place(BuildingKind.Turret, 20, 15)); }
        }
        foreach (var building in world.Buildings)
            if (!building.IsSold && building.Health < building.Definition.MaximumHealth && world.Credits >= building.Definition.RepairCost)
            { submit(WorldCommand.Repair(building.Id)); }
    }
    }
}
