﻿using System.Xml.Serialization;
using Crpg.Module.Common;
using Crpg.Module.Rewards;
using NetworkMessages.FromServer;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;
using TaleWorlds.ObjectSystem;

namespace Crpg.Module.Modes.Dtv;

internal class CrpgDtvServer : MissionMultiplayerGameModeBase
{
    private const int RewardMultiplier = 2;
    private const int MapDuration = 60 * 120;
    private const int BoulderRefillTime = 30;
    private const int FirePotRefillTime = 60;

    private readonly CrpgRewardServer _rewardServer;
    private readonly CrpgDtvData _dtvData;
    private readonly List<Agent> _mountsToKill = new();

    private int _currentRound;
    private int _currentRoundDefendersCount;
    private int _currentWave;
    private bool _gameStarted;
    private bool _waveStarted;
    private bool _timerExpired;
    private MissionTimer? _waveStartTimer;
    private MissionTimer? _endGameTimer;
    private MissionTimer _refillBouldersTimer = default!;
    private MissionTimer _refillFirePotsTimer = default!;
    private MissionTime _currentRoundStartTime;

    public CrpgDtvServer(CrpgRewardServer rewardServer)
    {
        _rewardServer = rewardServer;
        _dtvData = ReadDtvData();
        _gameStarted = false;
        _currentRound = -1;
        _timerExpired = false;
    }

    public override bool IsGameModeHidingAllAgentVisuals => true;
    public override bool IsGameModeUsingOpposingTeams => true;
    public override bool AllowCustomPlayerBanners() => false;
    public override bool UseRoundController() => false;

    private CrpgDtvSpawningBehavior SpawningBehavior => (CrpgDtvSpawningBehavior)SpawnComponent.SpawningBehavior;
    private int RoundsCount => _dtvData.Rounds.Count;
    private CrpgDtvRound CurrentRoundData => _dtvData.Rounds[_currentRound];
    private CrpgDtvWave CurrentWaveData => _dtvData.Rounds[_currentRound].Waves[_currentWave];
    private int WavesCountForCurrentRound => CurrentRoundData.Waves.Count;

    [Flags]
    private enum StonePileRegenerateOptions
    {
        All = 0x0,
        Pots = 0x1,
        Boulders = 0x2,
    }

    public override MultiplayerGameType GetMissionType()
    {
        return MultiplayerGameType.Battle;
    }

    public override void AfterStart()
    {
        base.AfterStart();
        AddTeams();
        _refillBouldersTimer = new(BoulderRefillTime);
        _refillFirePotsTimer = new(FirePotRefillTime);
    }

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();
        MissionLobbyComponent.CurrentMultiplayerStateChanged += RewardIfEndOfMission;
    }

    public override void OnRemoveBehavior()
    {
        base.OnRemoveBehavior();
        MissionLobbyComponent.CurrentMultiplayerStateChanged -= RewardIfEndOfMission;
    }

    public override bool CheckForWarmupEnd()
    {
        return true;
    }

    public override void OnPeerChangedTeam(NetworkCommunicator networkPeer, Team oldTeam, Team newTeam)
    {
        var missionPeer = networkPeer.GetComponent<MissionPeer>();
        var crpgPeer = networkPeer.GetComponent<CrpgPeer>();
        if (missionPeer == null)
        {
            return;
        }

        if (newTeam != Mission.SpectatorTeam)
        {
            missionPeer.Team = Mission.DefenderTeam;
            return;
        }

        if (crpgPeer != null)
        {
            crpgPeer.LastSpawnInfo = null;
        }
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Synchronize health with all clients to make the spectator health bar work.
        agent.UpdateSyncHealthToAllClients(true);
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (MissionLobbyComponent.CurrentMultiplayerState != MissionLobbyComponent.MultiplayerGameState.Playing
            || !CanGameModeSystemsTickThisFrame)
        {
            return;
        }

        if (_endGameTimer != null)
        {
            if (_endGameTimer.Check())
            {
                _endGameTimer = null;
                MissionLobbyComponent.SetStateEndingAsServer();
            }

            // If the game is ending don't process the tick any further.
            return;
        }

        if (_refillBouldersTimer.Check(true))
        {
            RegenerateStonePiles(StonePileRegenerateOptions.Boulders);
        }

        if (_refillFirePotsTimer.Check(true))
        {
            RegenerateStonePiles(StonePileRegenerateOptions.Pots);
        }

        if (!_gameStarted)
        {
            _gameStarted = true;
            SetTimeLimit();
            ClearPeerCounts();
            Mission.GetMissionBehavior<MissionScoreboardComponent>().ResetBotScores();
            StartNextRound();
        }
        else if (_waveStarted)
        {
            CheckForWaveEnd();
        }
        else if (_waveStartTimer != null && _waveStartTimer.Check())
        {
            _waveStartTimer = null;
            StartNextWave();
        }
    }

    public void RewardIfEndOfMission(MissionLobbyComponent.MultiplayerGameState newState)
    {
        if (!_timerExpired && TimerComponent.CheckIfTimerPassed() && newState == MissionLobbyComponent.MultiplayerGameState.Ending) // Award players if timer expires
        {
            _timerExpired = true;
            float roundDuration = _currentRoundStartTime.ElapsedSeconds;
            _ = _rewardServer.UpdateCrpgUsersAsync(
                durationRewarded: ComputeRoundReward(CurrentRoundData, wavesWon: Math.Max(_currentWave, 0)),
                durationUpkeep: roundDuration,
                updateUserStats: false,
                constantMultiplier: RewardMultiplier);
        }
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        if (!_gameStarted)
        {
            return;
        }

        if (affectedAgent.IsAIControlled && affectedAgent.Team == Mission.DefenderTeam) // VIP under attack
        {
            SendDataToPeers(new CrpgDtvGameEnd { VipDead = true, VipAgentIndex = affectedAgent.Index });
        }
    }

    public override void OnScoreHit(
        Agent affectedAgent,
        Agent affectorAgent,
        WeaponComponentData attackerWeapon,
        bool isBlocked,
        bool isSiegeEngineHit,
        in Blow blow,
        in AttackCollisionData collisionData,
        float damagedHp,
        float hitDistance,
        float shotDifficulty)
    {
        if (!_gameStarted)
        {
            return;
        }

        if (affectedAgent.IsAIControlled && affectedAgent.Team == Mission.DefenderTeam) // VIP under attack
        {
            SendDataToPeers(new CrpgDtvVipUnderAttackMessage { AgentAttackerIndex = affectorAgent.Index, AgentVictimIndex = affectedAgent.Index });
        }
    }

    protected override void HandleNewClientAfterSynchronized(NetworkCommunicator networkPeer)
    {
        if (!_gameStarted)
        {
            return;
        }

        GameNetwork.BeginModuleEventAsServer(networkPeer);
        GameNetwork.WriteMessage(new CrpgDtvSetTimerMessage
        {
            StartTime = (int)TimerComponent.GetCurrentTimerStartTime().ToSeconds,
            Duration = MapDuration,
        });
        GameNetwork.EndModuleEventAsServer();
        GameNetwork.BeginModuleEventAsServer(networkPeer);
        GameNetwork.WriteMessage(new CrpgDtvCurrentProgressMessage
        {
            Wave = _currentWave,
            Round = _currentRound,
        });
        GameNetwork.EndModuleEventAsServer();
    }

    /// <summary>Work around the 60 minutes limit of MapTimeLimit.</summary>
    private void SetTimeLimit()
    {
        TimerComponent.StartTimerAsServer(MapDuration);
        SendDataToPeers(new CrpgDtvSetTimerMessage
        {
            StartTime = (int)TimerComponent.GetCurrentTimerStartTime().ToSeconds,
            Duration = MapDuration,
        });
    }

    private void StartNextRound()
    {
        _currentRound += 1;
        _currentWave = -1;
        SpawningBehavior.RequestSpawnSessionForRoundStart(firstRound: _currentRound == 0);
        SendDataToPeers(new CrpgDtvRoundStartMessage { Round = _currentRound });

        for (int i = _mountsToKill.Count - 1; i >= 0; i--) // kill mounts marked for death
        {
            if (_mountsToKill[i].RiderAgent == null)
            {
                DamageHelper.DamageAgent(_mountsToKill[i], 500);
            }

            _mountsToKill.Remove(_mountsToKill[i]);
        }

        foreach (var mount in Mission.MountsWithoutRiders) // force mounts to flee and mark them to die next round
        {
            Agent mountAgent = mount.Key;
            _mountsToKill.Add(mountAgent);
            mountAgent.CommonAIComponent.Panic();
        }

        _currentRoundStartTime = MissionTime.Now;
        _waveStartTimer = new MissionTimer(15f);
        _waveStarted = false;
    }

    private void StartNextWave()
    {
        _currentWave += 1;
        RefillDefendersHealthPointsAndAmmo();
        _currentRoundDefendersCount = _currentWave == 0 ? GetDefendersCount() : _currentRoundDefendersCount;
        SpawningBehavior.RequestSpawnSessionForWaveStart(CurrentWaveData, _currentRoundDefendersCount);
        SendDataToPeers(new CrpgDtvWaveStartMessage { Wave = _currentWave });
        _waveStarted = true;
    }

    private void CheckForWaveEnd()
    {
        bool vipDead = !Mission.DefenderTeam.HasBots;
        bool defendersDepleted = Mission.DefenderTeam.ActiveAgents.Count == (vipDead ? 0 : 1);
        float roundDuration = _currentRoundStartTime.ElapsedSeconds;

        if (defendersDepleted)
        {
            SendDataToPeers(new CrpgDtvGameEnd { VipDead = false });
        }

        if (vipDead || defendersDepleted)
        {
            _ = _rewardServer.UpdateCrpgUsersAsync(
                durationRewarded: ComputeRoundReward(CurrentRoundData, wavesWon: _currentWave),
                durationUpkeep: roundDuration,
                updateUserStats: false,
                constantMultiplier: RewardMultiplier);
            EndGame(Mission.AttackerTeam);
            return;
        }

        bool attackersDepleted = !Mission.AttackerTeam.HasBots;
        if (!attackersDepleted)
        {
            return;
        }

        if (_currentWave < WavesCountForCurrentRound - 1)
        {
            StartNextWave();
            return;
        }

        _ = _rewardServer.UpdateCrpgUsersAsync(
            durationRewarded: ComputeRoundReward(CurrentRoundData, wavesWon: _currentWave + 1),
            durationUpkeep: roundDuration,
            updateUserStats: false,
            constantMultiplier: RewardMultiplier);

        if (_currentRound < RoundsCount - 1)
        {
            StartNextRound();
        }
        else
        {
            EndGame(Mission.DefenderTeam);
        }
    }

    private void EndGame(Team winnerTeam)
    {
        Mission.GetMissionBehavior<MissionScoreboardComponent>().ChangeTeamScore(winnerTeam, 1);
        _endGameTimer = new MissionTimer(8f);
    }

    private void RefillDefendersHealthPointsAndAmmo()
    {
        foreach (Agent agent in Mission.DefenderTeam.ActiveAgents)
        {
            agent.Health = agent.HealthLimit;
            if (agent.HasMount)
            {
                agent.MountAgent.Health = agent.MountAgent.HealthLimit;
            }

            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i += 1)
            {
                var weapon = agent.Equipment[i];
                if (!weapon.IsEmpty && (weapon.IsAnyConsumable() || weapon.CurrentUsageItem.IsShield))
                {
                    agent.SetWeaponAmountInSlot(i, weapon.ModifiedMaxAmount, false);
                }
            }
        }
    }

    private void RegenerateStonePiles(StonePileRegenerateOptions flag = StonePileRegenerateOptions.All)
    {
        string itemID = string.Empty;

        if (flag == StonePileRegenerateOptions.Pots)
        {
            itemID = "pot";
        }
        else if (flag == StonePileRegenerateOptions.Boulders)
        {
            itemID = "boulder";
        }

        foreach (StonePile stonePile in Mission.MissionObjects.FindAllWithType<StonePile>())
        {
            if (flag != StonePileRegenerateOptions.All && stonePile.GivenItemID != itemID)
            {
               continue;
            }

            int ammoCount = stonePile.AmmoCount;

            if (ammoCount != stonePile.StartingAmmoCount)
            {
                int newAmmoCount = ammoCount + 1;
                newAmmoCount = Math.Min(newAmmoCount, stonePile.StartingAmmoCount);
                stonePile.SetAmmo(newAmmoCount);
                stonePile.Activate();

                GameNetwork.BeginBroadcastModuleEvent();
                GameNetwork.WriteMessage(new SetStonePileAmmo(stonePile.Id, newAmmoCount));
                GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.AddToMissionRecord);
            }
        }

        foreach (RangedSiegeWeapon siegeWeapon in Mission.MissionObjects.FindAllWithType<RangedSiegeWeapon>())
        {
            if (flag == StonePileRegenerateOptions.Pots)
            {
                if (siegeWeapon is not FireMangonel)
                {
                    continue;
                }
            }
            else if (flag == StonePileRegenerateOptions.Boulders)
            {
                if (siegeWeapon is FireMangonel)
                {
                    continue;
                }
            }

            int ammoCount2 = siegeWeapon.AmmoCount;
            if (ammoCount2 != siegeWeapon.startingAmmoCount)
            {
                int newAmmoCount2 = siegeWeapon.AmmoCount + 1;
                newAmmoCount2 = Math.Min(newAmmoCount2, siegeWeapon.startingAmmoCount);
                siegeWeapon.SetAmmo(newAmmoCount2);
                siegeWeapon.Activate();

                GameNetwork.BeginBroadcastModuleEvent();
                GameNetwork.WriteMessage(new SetRangedSiegeWeaponAmmo(siegeWeapon.Id, newAmmoCount2));
                GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.AddToMissionRecord, null);
            }
        }
    }

    private int GetDefendersCount()
    {
        int defendersCount = 0;
        foreach (NetworkCommunicator networkPeer in GameNetwork.NetworkPeers)
        {
            var crpgPeer = networkPeer.GetComponent<CrpgPeer>();
            if (!networkPeer.IsSynchronized
                || crpgPeer.LastSpawnInfo == null)
            {
                continue;
            }

            defendersCount += 1;
        }

        return defendersCount;
    }

    private float ComputeRoundReward(CrpgDtvRound data, int wavesWon)
    {
        float defendersScale = 1 + (_currentRoundDefendersCount - 1) * 0.02f;
        float lostRoundPenalty = (float)wavesWon / data.Waves.Count;
        return data.Reward * defendersScale * lostRoundPenalty;
    }

    private void SendDataToPeers(GameNetworkMessage message)
    {
        GameNetwork.BeginBroadcastModuleEvent();
        GameNetwork.WriteMessage(message);
        GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
    }

    private void AddTeams()
    {
        BasicCultureObject attackerTeamCulture = MBObjectManager.Instance.GetObject<BasicCultureObject>(MultiplayerOptions.OptionType.CultureTeam1.GetStrValue());
        Banner bannerTeam1 = new(attackerTeamCulture.BannerKey, attackerTeamCulture.BackgroundColor1, attackerTeamCulture.ForegroundColor1);
        Mission.Teams.Add(BattleSideEnum.Attacker, attackerTeamCulture.BackgroundColor1, attackerTeamCulture.ForegroundColor1, bannerTeam1, false, true);
        BasicCultureObject defenderTeamCulture = MBObjectManager.Instance.GetObject<BasicCultureObject>(MultiplayerOptions.OptionType.CultureTeam2.GetStrValue());
        Banner bannerTeam2 = new(defenderTeamCulture.BannerKey, defenderTeamCulture.BackgroundColor2, defenderTeamCulture.ForegroundColor2);
        Mission.Teams.Add(BattleSideEnum.Defender, defenderTeamCulture.BackgroundColor2, defenderTeamCulture.ForegroundColor2, bannerTeam2, false, true);
    }

    private CrpgDtvData ReadDtvData()
    {
        XmlSerializer ser = new(typeof(CrpgDtvData));
        using StreamReader sr = new(ModuleHelper.GetXmlPath("Crpg", "dtv\\dtv_data"));
        return (CrpgDtvData)ser.Deserialize(sr);
    }
}
