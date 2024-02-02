using Crpg.Module.Helpers;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.DedicatedCustomServer;
using TaleWorlds.MountAndBlade.ListedServer;
using TaleWorlds.MountAndBlade.Multiplayer.NetworkComponents;

namespace Crpg.Module.Common;

/// <summary>
/// If voting is enabled, allow voting for the X next maps of the pool; otherwise shuffle once the pool and picks the maps sequentially.
/// </summary>
/// <remarks>
/// I could not find a way to branch to game start/end so I branched to <see cref="OnEndMission"/>. It means that the
/// intermission screen when the game start, will show all maps. It's ok since usually nobody is connected when the
/// server starts.
/// </remarks>
internal class MapPoolComponent : MissionBehavior
{
    private static int nextMapId;

    private string? _forcedNextMap;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public void ForceNextMap(string map)
    {
        if (ListedServerCommandManager.ServerSideIntermissionManager.AutomatedMapPool.Contains(map))
        {
            return;
        }

        _forcedNextMap = map;
    }
    public override void OnAfterMissionCreated()
    {
        // For some reason OnAfterMissionCreated() and OnAfterMissionEnding() is called twice. So this is to make the code idempotent.
        if (MultiplayerOptions.OptionType.Map.GetStrValue(MultiplayerOptions.MultiplayerOptionsAccessMode.NextMapOptions) != MultiplayerOptions.OptionType.Map.GetStrValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions))
        {
            return;
        }

        nextMapId = (nextMapId + 1) % ListedServerCommandManager.ServerSideIntermissionManager.AutomatedMapPool.Count;
        string nextMap = _forcedNextMap ?? ListedServerCommandManager.ServerSideIntermissionManager.AutomatedMapPool[nextMapId];
        MultiplayerOptions.OptionType.Map.SetValue(nextMap, MultiplayerOptions.MultiplayerOptionsAccessMode.NextMapOptions);
        _forcedNextMap = null;
    }
}
