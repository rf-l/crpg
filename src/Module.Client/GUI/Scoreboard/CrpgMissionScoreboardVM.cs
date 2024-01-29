﻿using System;
using System.Collections.Generic;
using System.Text;
using Crpg.Module.Common.Commander;
using Crpg.Module.Gui;
using TaleWorlds.CampaignSystem.ViewModelCollection.Input;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection.Scoreboard;
using TaleWorlds.PlatformService;
using TaleWorlds.PlayerServices;

namespace Crpg.Module.GUI.Scoreboard;
internal class CrpgMissionScoreboardVM : ViewModel
{
    private const float AttributeRefreshDuration = 1f;

    private const float PermissionCheckDuration = 45f;

    private ChatBox _chatBox;

    private readonly Dictionary<BattleSideEnum, CrpgScoreboardSideVM> _missionSides = default!;

    private readonly MissionScoreboardComponent _missionScoreboardComponent = default!;

    private readonly MultiplayerPollComponent _missionPollComponent = default!;

    private readonly CrpgCommanderPollComponent? _commanderPollComponent;

    private readonly CrpgCommanderBehaviorClient? _commanderClient;

    private VoiceChatHandler _voiceChatHandler = default!;

    private MultiplayerPermissionHandler _permissionHandler = default!;

    private readonly Mission _mission;

    private float _attributeRefreshTimeElapsed;

    private bool _hasMutedAll;

    private bool _canStartKickPolls;
    private bool _canStartCommanderPolls = false;

    private TextObject _muteAllText = new("{=AZSbwcG5}Mute All", null);

    private TextObject _unmuteAllText = new("{=SzRVIPeZ}Unmute All", null);

    private bool _isActive;

    private InputKeyItemVM _showMouseKey = default!;

    private CrpgScoreboardEndOfBattleVM _endOfBattle = default!;

    private MBBindingList<CrpgScoreboardSideVM> _sides = default!;

    private MBBindingList<StringPairItemWithActionVM> _playerActionList = default!;

    private string _spectators = default!;

    private string _missionName = default!;

    private string _gameModeText = default!;

    private string _mapName = default!;

    private string _serverName = default!;

    private bool _isBotsEnabled;

    private bool _isSingleSide;

    private bool _isInitalizationOver;

    private bool _isUpdateOver;

    private bool _isMouseEnabled;

    private bool _isPlayerActionsActive;

    private string _toggleMuteText = default!;
    private ImageIdentifierVM? _allyBanner;
    private ImageIdentifierVM? _enemyBanner;
    public CrpgMissionScoreboardVM(bool isSingleTeam, Mission mission)
    {
        _chatBox = Game.Current.GetGameHandler<ChatBox>();
        _chatBox.OnPlayerMuteChanged += OnPlayerMuteChanged;
        _mission = mission;
        MissionLobbyComponent missionBehavior = mission.GetMissionBehavior<MissionLobbyComponent>();
        _missionScoreboardComponent = mission.GetMissionBehavior<MissionScoreboardComponent>();
        _voiceChatHandler = _mission.GetMissionBehavior<VoiceChatHandler>();
        _permissionHandler = GameNetwork.GetNetworkComponent<MultiplayerPermissionHandler>();
        if (_voiceChatHandler != null)
        {
            _voiceChatHandler.OnPeerMuteStatusUpdated += OnPeerMuteStatusUpdated;
        }

        if (_permissionHandler != null)
        {
            _permissionHandler.OnPlayerPlatformMuteChanged += OnPlayerPlatformMuteChanged;
        }

        _canStartKickPolls = MultiplayerOptions.OptionType.AllowPollsToKickPlayers.GetBoolValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions);
        if (_canStartKickPolls)
        {
            _missionPollComponent = mission.GetMissionBehavior<MultiplayerPollComponent>();
        }

        _commanderClient = mission.GetMissionBehavior<CrpgCommanderBehaviorClient>();
        _commanderPollComponent = mission.GetMissionBehavior<CrpgCommanderPollComponent>();
        _canStartCommanderPolls = false;
        if (_commanderClient != null && _commanderPollComponent != null)
        {
            _canStartCommanderPolls = true;
        }

        EndOfBattle = new CrpgScoreboardEndOfBattleVM(mission, _missionScoreboardComponent, isSingleTeam);
        PlayerActionList = new MBBindingList<StringPairItemWithActionVM>();
        Sides = new MBBindingList<CrpgScoreboardSideVM>();
        _missionSides = new Dictionary<BattleSideEnum, CrpgScoreboardSideVM>();
        IsSingleSide = isSingleTeam;
        InitSides();
        GameKey gameKey = HotKeyManager.GetCategory("ScoreboardHotKeyCategory").GetGameKey(35);
        ShowMouseKey = InputKeyItemVM.CreateFromGameKey(gameKey, false);
        _missionScoreboardComponent.OnPlayerSideChanged += OnPlayerSideChanged;
        _missionScoreboardComponent.OnPlayerPropertiesChanged += OnPlayerPropertiesChanged;
        _missionScoreboardComponent.OnBotPropertiesChanged += OnBotPropertiesChanged;
        _missionScoreboardComponent.OnRoundPropertiesChanged += OnRoundPropertiesChanged;
        _missionScoreboardComponent.OnScoreboardInitialized += OnScoreboardInitialized;
        _missionScoreboardComponent.OnMVPSelected += OnMVPSelected;
        MissionName = "";
        IsBotsEnabled = missionBehavior.MissionType == MultiplayerGameType.Captain || missionBehavior.MissionType == MultiplayerGameType.Battle;
        RefreshValues();
        var customBanners = Mission.Current.GetMissionBehavior<CrpgCustomTeamBannersAndNamesClient>();
        if (customBanners != null)
        {
            customBanners.BannersChanged += HandleBannerChange;
        }
    }

    private void HandleBannerChange(BannerCode attackerBanner, BannerCode defenderBanner, string attackerName, string defenderName)
    {
        AllyBanner = new(GameNetwork.MyPeer.GetComponent<MissionPeer>()?.Team?.Side == BattleSideEnum.Attacker ? attackerBanner : defenderBanner, true);
        EnemyBanner = new(GameNetwork.MyPeer.GetComponent<MissionPeer>()?.Team?.Side == BattleSideEnum.Defender ? attackerBanner : defenderBanner, true);
    }

    public override void RefreshValues()
    {
        base.RefreshValues();
        MissionLobbyComponent missionBehavior = _mission.GetMissionBehavior<MissionLobbyComponent>();
        UpdateToggleMuteText();
        GameModeText = GameTexts.FindText("str_multiplayer_game_type", missionBehavior.MissionType.ToString()).ToString().ToLower();
        EndOfBattle.RefreshValues();
        Sides.ApplyActionOnAllItems(delegate (CrpgScoreboardSideVM x)
        {
            x.RefreshValues();
        });
        MapName = GameTexts.FindText("str_multiplayer_scene_name", missionBehavior.Mission.SceneName).ToString();
        ServerName = MultiplayerOptions.OptionType.ServerName.GetStrValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions);
        InputKeyItemVM showMouseKey = ShowMouseKey;
        if (showMouseKey == null)
        {
            return;
        }

        showMouseKey.RefreshValues();
    }

    private void OnPlayerPlatformMuteChanged(PlayerId playerId, bool isPlayerMuted)
    {
        foreach (CrpgScoreboardSideVM crpgScoreboardSideVM in Sides)
        {
            foreach (MissionScoreboardPlayerVM missionScoreboardPlayerVM in crpgScoreboardSideVM.Players)
            {
                if (missionScoreboardPlayerVM.Peer.Peer.Id.Equals(playerId))
                {
                    missionScoreboardPlayerVM.UpdateIsMuted();
                    return;
                }
            }
        }
    }

    private void OnPlayerMuteChanged(PlayerId playerId, bool isMuted)
    {
        foreach (CrpgScoreboardSideVM crpgScoreboardSideVM in Sides)
        {
            foreach (MissionScoreboardPlayerVM missionScoreboardPlayerVM in crpgScoreboardSideVM.Players)
            {
                if (missionScoreboardPlayerVM.Peer.Peer.Id.Equals(playerId))
                {
                    missionScoreboardPlayerVM.UpdateIsMuted();
                    return;
                }
            }
        }
    }

    private void ExecutePopulateActionList(MissionScoreboardPlayerVM player)
    {
        PlayerActionList.Clear();
        if (player.Peer != null && !player.IsMine && !player.IsBot)
        {
            PlayerId id = player.Peer.Peer.Id;
            bool flag = _chatBox.IsPlayerMutedFromGame(id);
            bool flag2 = PermaMuteList.IsPlayerMuted(id);
            bool flag3 = _chatBox.IsPlayerMutedFromPlatform(id);
            bool isMutedFromPlatform = player.Peer.IsMutedFromPlatform;
            if (!flag3)
            {
                if (!flag2)
                {
                    if (PlatformServices.Instance.IsPermanentMuteAvailable)
                    {
                        PlayerActionList.Add(new StringPairItemWithActionVM(new Action<object>(ExecutePermanentlyMute), new TextObject("{=77jmd4QF}Mute Permanently", null).ToString(), "PermanentMute", player));
                    }

                    string definition = flag ? GameTexts.FindText("str_mp_scoreboard_context_unmute_text", null).ToString() : GameTexts.FindText("str_mp_scoreboard_context_mute_text", null).ToString();
                    PlayerActionList.Add(new StringPairItemWithActionVM(new Action<object>(ExecuteMute), definition, flag ? "UnmuteText" : "MuteText", player));
                }
                else
                {
                    PlayerActionList.Add(new StringPairItemWithActionVM(new Action<object>(ExecuteLiftPermanentMute), new TextObject("{=CIVPNf2d}Remove Permanent Mute", null).ToString(), "UnmuteText", player));
                }
            }

            if (player.IsTeammate)
            {
                if (!isMutedFromPlatform && _voiceChatHandler != null && !flag2)
                {
                    string definition2 = player.Peer.IsMuted ? GameTexts.FindText("str_mp_scoreboard_context_unmute_voice", null).ToString() : GameTexts.FindText("str_mp_scoreboard_context_mute_voice", null).ToString();
                    PlayerActionList.Add(new StringPairItemWithActionVM(new Action<object>(ExecuteMuteVoice), definition2, player.Peer.IsMuted ? "UnmuteVoice" : "MuteVoice", player));
                }

                if (_canStartKickPolls)
                {
                    PlayerActionList.Add(new StringPairItemWithActionVM(new Action<object>(ExecuteKick), GameTexts.FindText("str_mp_scoreboard_context_kick", null).ToString(), "StartKickPoll", player));
                }

                if (_canStartCommanderPolls)
                {
                    bool isCommander = _commanderClient!.IsPeerCommander(player.Peer);
                    string definition3 = isCommander ? GameTexts.FindText("str_mp_scoreboard_context_demote_commander", null).ToString() : GameTexts.FindText("str_mp_scoreboard_context_promote_commander", null).ToString();
                    PlayerActionList.Add(new StringPairItemWithActionVM(new Action<object>(ExecuteCommander), definition3, isCommander ? "DemoteCommander" : "PromoteCommander", player));
                }
            }

            StringPairItemWithActionVM stringPairItemWithActionVM = new(new Action<object>(ExecuteReport), GameTexts.FindText("str_mp_scoreboard_context_report", null).ToString(), "Report", player);
            if (MultiplayerReportPlayerManager.IsPlayerReportedOverLimit(id))
            {
                stringPairItemWithActionVM.IsEnabled = false;
                stringPairItemWithActionVM.Hint.HintText = new TextObject("{=klkYFik9}You've already reported this player.", null);
            }

            PlayerActionList.Add(stringPairItemWithActionVM);
            MultiplayerPlayerContextMenuHelper.AddMissionViewProfileOptions(player, PlayerActionList);
        }

        if (PlayerActionList.Count > 0)
        {
            IsPlayerActionsActive = false;
            IsPlayerActionsActive = true;
        }
    }

    public void SetMouseState(bool isMouseVisible)
    {
        IsMouseEnabled = isMouseVisible;
    }

    private void ExecuteReport(object playerObj)
    {
        if (playerObj is not MissionScoreboardPlayerVM missionScoreboardPlayerVM)
        {
            return;
        }

        MultiplayerReportPlayerManager.RequestReportPlayer(NetworkMain.GameClient.CurrentMatchId, missionScoreboardPlayerVM.Peer.Peer.Id, missionScoreboardPlayerVM.Peer.DisplayedName, true);
    }

    private void ExecuteCommander(object playerObj)
    {
        if (playerObj is not MissionScoreboardPlayerVM missionScoreboardPlayerVM)
        {
            return;
        }

        if (_mission.GetMissionBehavior<MissionLobbyComponent>().IsInWarmup)
        {
            MBInformationManager.AddQuickInformation(new TextObject("{=wQOq8JIE}You cannot vote for a Commander during warmup!"), 0, null, null);
            return;
        }

        bool isCommander = _commanderClient!.IsPeerCommander(missionScoreboardPlayerVM.Peer);
        _commanderPollComponent!.RequestCommanderPoll(missionScoreboardPlayerVM.Peer.GetNetworkPeer(), isCommander);
    }

    private void ExecuteMute(object playerObj)
    {
        if (playerObj is not MissionScoreboardPlayerVM missionScoreboardPlayerVM)
        {
            return;
        }

        bool flag = _chatBox.IsPlayerMutedFromGame(missionScoreboardPlayerVM.Peer.Peer.Id);
        _chatBox.SetPlayerMuted(missionScoreboardPlayerVM.Peer.Peer.Id, !flag);
        GameTexts.SetVariable("PLAYER_NAME", missionScoreboardPlayerVM.Peer.DisplayedName);
        InformationManager.DisplayMessage(new InformationMessage((!flag) ? GameTexts.FindText("str_mute_notification", null).ToString() : GameTexts.FindText("str_unmute_notification", null).ToString()));
    }

    private void ExecuteMuteVoice(object playerObj)
    {
        MissionScoreboardPlayerVM? missionScoreboardPlayerVM = playerObj as MissionScoreboardPlayerVM;
        missionScoreboardPlayerVM?.Peer.SetMuted(!missionScoreboardPlayerVM.Peer.IsMuted);
        missionScoreboardPlayerVM?.UpdateIsMuted();
    }

    private void ExecutePermanentlyMute(object playerObj)
    {
        if (playerObj is not MissionScoreboardPlayerVM missionScoreboardPlayerVM)
        {
            return;
        }

        PermaMuteList.MutePlayer(missionScoreboardPlayerVM.Peer.Peer.Id, missionScoreboardPlayerVM.Peer.Name);
        missionScoreboardPlayerVM.Peer.SetMuted(true);
        missionScoreboardPlayerVM.UpdateIsMuted();
        GameTexts.SetVariable("PLAYER_NAME", missionScoreboardPlayerVM.Peer.DisplayedName);
        InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_permanent_mute_notification", null).ToString()));
    }

    private void ExecuteLiftPermanentMute(object playerObj)
    {
        if (playerObj is not MissionScoreboardPlayerVM missionScoreboardPlayerVM)
        {
            return;
        }

        PermaMuteList.RemoveMutedPlayer(missionScoreboardPlayerVM.Peer.Peer.Id);
        missionScoreboardPlayerVM.Peer.SetMuted(false);
        missionScoreboardPlayerVM.UpdateIsMuted();
        GameTexts.SetVariable("PLAYER_NAME", missionScoreboardPlayerVM.Peer.DisplayedName);
        InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_unmute_notification", null).ToString()));
    }

    private void ExecuteKick(object playerObj)
    {
        if (playerObj is not MissionScoreboardPlayerVM missionScoreboardPlayerVM)
        {
            return;
        }

        if (_canStartCommanderPolls)
        {
            if (_commanderPollComponent!.IsPollOngoing())
            {
                _commanderPollComponent.RejectPoll(MultiplayerPollRejectReason.HasOngoingPoll);
                return;
            }
        }

        _missionPollComponent.RequestKickPlayerPoll(missionScoreboardPlayerVM.Peer.GetNetworkPeer(), false);
    }

    public void Tick(float dt)
    {
        if (IsActive)
        {
            CrpgScoreboardEndOfBattleVM endOfBattle = EndOfBattle;
            endOfBattle?.Tick(dt);

            CheckAttributeRefresh(dt);
            foreach (CrpgScoreboardSideVM crpgScoreboardSideVM in Sides)
            {
                crpgScoreboardSideVM.Tick(dt);
            }

            foreach (CrpgScoreboardSideVM crpgScoreboardSideVM2 in Sides)
            {
                foreach (MissionScoreboardPlayerVM missionScoreboardPlayerVM in crpgScoreboardSideVM2.Players)
                {
                    missionScoreboardPlayerVM.RefreshDivision(IsSingleSide);
                }
            }
        }
    }

    private void CheckAttributeRefresh(float dt)
    {
        _attributeRefreshTimeElapsed += dt;
        if (_attributeRefreshTimeElapsed >= 1f)
        {
            UpdateSideAllPlayersAttributes(BattleSideEnum.Attacker);
            UpdateSideAllPlayersAttributes(BattleSideEnum.Defender);
            _attributeRefreshTimeElapsed = 0f;
        }
    }

    public override void OnFinalize()
    {
        base.OnFinalize();
        _missionScoreboardComponent.OnPlayerSideChanged -= OnPlayerSideChanged;
        _missionScoreboardComponent.OnPlayerPropertiesChanged -= OnPlayerPropertiesChanged;
        _missionScoreboardComponent.OnBotPropertiesChanged -= OnBotPropertiesChanged;
        _missionScoreboardComponent.OnRoundPropertiesChanged -= OnRoundPropertiesChanged;
        _missionScoreboardComponent.OnScoreboardInitialized -= OnScoreboardInitialized;
        _missionScoreboardComponent.OnMVPSelected -= OnMVPSelected;
        _chatBox.OnPlayerMuteChanged -= OnPlayerMuteChanged;
        if (_voiceChatHandler != null)
        {
            _voiceChatHandler.OnPeerMuteStatusUpdated -= OnPeerMuteStatusUpdated;
        }

        foreach (CrpgScoreboardSideVM crpgScoreboardSideVM in Sides)
        {
            crpgScoreboardSideVM.OnFinalize();
        }
    }

    private void UpdateSideAllPlayersAttributes(BattleSideEnum battleSide)
    {
        MissionScoreboardComponent.MissionScoreboardSide missionScoreboardSide = _missionScoreboardComponent.Sides.FirstOrDefault((MissionScoreboardComponent.MissionScoreboardSide s) => s != null && s.Side == battleSide);
        if (missionScoreboardSide != null)
        {
            foreach (MissionPeer client in missionScoreboardSide.Players)
            {
                OnPlayerPropertiesChanged(battleSide, client);
            }
        }
    }

    public void OnPlayerSideChanged(Team curTeam, Team nextTeam, MissionPeer client)
    {
        if (client.IsMine && nextTeam != null && IsSideValid(nextTeam.Side))
        {
            InitSides();
            return;
        }

        if (curTeam != null && IsSideValid(curTeam.Side))
        {
            _missionSides[_missionScoreboardComponent.GetSideSafe(curTeam.Side).Side].RemovePlayer(client);
        }

        if (nextTeam != null && IsSideValid(nextTeam.Side))
        {
            _missionSides[_missionScoreboardComponent.GetSideSafe(nextTeam.Side).Side].AddPlayer(client);
        }
    }

    private void OnRoundPropertiesChanged()
    {
        foreach (CrpgScoreboardSideVM crpgScoreboardSideVM in _missionSides.Values)
        {
            crpgScoreboardSideVM.UpdateRoundAttributes();
        }
    }

    private void OnPlayerPropertiesChanged(BattleSideEnum side, MissionPeer client)
    {
        if (IsSideValid(side))
        {
            _missionSides[_missionScoreboardComponent.GetSideSafe(side).Side].UpdatePlayerAttributes(client);
        }
    }

    private void OnBotPropertiesChanged(BattleSideEnum side)
    {
        BattleSideEnum side2 = _missionScoreboardComponent.GetSideSafe(side).Side;
        if (IsSideValid(side2))
        {
            _missionSides[side2].UpdateBotAttributes();
        }
    }

    private void OnScoreboardInitialized()
    {
        InitSides();
    }

    private void OnMVPSelected(MissionPeer mvpPeer, int mvpCount)
    {
        foreach (CrpgScoreboardSideVM crpgScoreboardSideVM in Sides)
        {
            foreach (MissionScoreboardPlayerVM missionScoreboardPlayerVM in crpgScoreboardSideVM.Players)
            {
                if (missionScoreboardPlayerVM.Peer == mvpPeer)
                {
                    missionScoreboardPlayerVM.SetMVPBadgeCount(mvpCount);
                    break;
                }
            }
        }
    }

    private bool IsSideValid(BattleSideEnum side)
    {
        if (IsSingleSide)
        {
            return _missionScoreboardComponent != null && side != BattleSideEnum.None && side != BattleSideEnum.NumSides;
        }

        return _missionScoreboardComponent != null && side != BattleSideEnum.None && side != BattleSideEnum.NumSides && _missionScoreboardComponent.Sides.Any((MissionScoreboardComponent.MissionScoreboardSide s) => s != null && s.Side == side);
    }

    private void InitSides()
    {
        Sides.Clear();
        _missionSides.Clear();
        if (IsSingleSide)
        {
            MissionScoreboardComponent.MissionScoreboardSide sideSafe = _missionScoreboardComponent.GetSideSafe(BattleSideEnum.Defender);
            CrpgScoreboardSideVM crpgScoreboardSideVM = new(sideSafe, new Action<MissionScoreboardPlayerVM>(ExecutePopulateActionList), IsSingleSide, false);
            Sides.Add(crpgScoreboardSideVM);
            _missionSides.Add(sideSafe.Side, crpgScoreboardSideVM);
            return;
        }

        NetworkCommunicator myPeer = GameNetwork.MyPeer;
        MissionPeer? missionPeer = myPeer?.GetComponent<MissionPeer>();
        BattleSideEnum firstSideToAdd = BattleSideEnum.Attacker;
        BattleSideEnum secondSideToAdd = BattleSideEnum.Defender;
        if (missionPeer != null)
        {
            Team team = missionPeer.Team;
            if (team != null && team.Side == BattleSideEnum.Defender)
            {
                firstSideToAdd = BattleSideEnum.Defender;
                secondSideToAdd = BattleSideEnum.Attacker;
            }
        }

        MissionScoreboardComponent.MissionScoreboardSide missionScoreboardSide = _missionScoreboardComponent.Sides.FirstOrDefault((MissionScoreboardComponent.MissionScoreboardSide s) => s != null && s.Side == firstSideToAdd);
        if (missionScoreboardSide != null)
        {
            CrpgScoreboardSideVM crpgScoreboardSideVM2 = new(missionScoreboardSide, new Action<MissionScoreboardPlayerVM>(ExecutePopulateActionList), IsSingleSide, false);
            Sides.Add(crpgScoreboardSideVM2);
            _missionSides.Add(missionScoreboardSide.Side, crpgScoreboardSideVM2);
        }

        missionScoreboardSide = _missionScoreboardComponent.Sides.FirstOrDefault((MissionScoreboardComponent.MissionScoreboardSide s) => s != null && s.Side == secondSideToAdd);
        if (missionScoreboardSide != null)
        {
            CrpgScoreboardSideVM crpgScoreboardSideVM3 = new(missionScoreboardSide, new Action<MissionScoreboardPlayerVM>(ExecutePopulateActionList), IsSingleSide, true);
            Sides.Add(crpgScoreboardSideVM3);
            _missionSides.Add(missionScoreboardSide.Side, crpgScoreboardSideVM3);
        }
    }

    private BattleSideEnum AllySide
    {
        get
        {
            BattleSideEnum result = BattleSideEnum.None;
            if (GameNetwork.IsMyPeerReady)
            {
                MissionPeer component = GameNetwork.MyPeer.GetComponent<MissionPeer>();
                if (component != null && component.Team != null)
                {
                    result = component.Team.Side;
                }
            }

            return result;
        }
    }

    private BattleSideEnum EnemySide
    {
        get
        {
            BattleSideEnum allySide = AllySide;
            if (allySide == BattleSideEnum.Defender)
            {
                return BattleSideEnum.Attacker;
            }

            if (allySide == BattleSideEnum.Attacker)
            {
                return BattleSideEnum.Defender;
            }

            Debug.FailedAssert("Ally side must be either Attacker or Defender", "C:\\Develop\\MB3\\Source\\Bannerlord\\TaleWorlds.MountAndBlade.ViewModelCollection\\Multiplayer\\Scoreboard\\MissionScoreboardVM.cs", "EnemySide", 517);
            return BattleSideEnum.None;
        }
    }

    public void DecreaseSpectatorCount(MissionPeer spectatedPeer)
    {
    }

    public void IncreaseSpectatorCount(MissionPeer spectatedPeer)
    {
    }

    public void ExecuteToggleMute()
    {
        foreach (CrpgScoreboardSideVM crpgScoreboardSideVM in Sides)
        {
            foreach (MissionScoreboardPlayerVM missionScoreboardPlayerVM in crpgScoreboardSideVM.Players)
            {
                if (!missionScoreboardPlayerVM.IsMine && missionScoreboardPlayerVM.Peer != null)
                {
                    _chatBox.SetPlayerMuted(missionScoreboardPlayerVM.Peer.Peer.Id, !_hasMutedAll);
                    missionScoreboardPlayerVM.Peer.SetMuted(!_hasMutedAll);
                    missionScoreboardPlayerVM.UpdateIsMuted();
                }
            }
        }

        _hasMutedAll = !_hasMutedAll;
        UpdateToggleMuteText();
    }

    private void UpdateToggleMuteText()
    {
        if (_hasMutedAll)
        {
            ToggleMuteText = _unmuteAllText.ToString();
            return;
        }

        ToggleMuteText = _muteAllText.ToString();
    }

    private void OnPeerMuteStatusUpdated(MissionPeer peer)
    {
        foreach (CrpgScoreboardSideVM crpgScoreboardSideVM in Sides)
        {
            foreach (MissionScoreboardPlayerVM missionScoreboardPlayerVM in crpgScoreboardSideVM.Players)
            {
                if (missionScoreboardPlayerVM.Peer == peer)
                {
                    missionScoreboardPlayerVM.UpdateIsMuted();
                    break;
                }
            }
        }
    }

    [DataSourceProperty]
    public ImageIdentifierVM? AllyBanner
    {
        get
        {
            return _allyBanner;
        }
        set
        {
            if (value == _allyBanner)
            {
                return;
            }

            _allyBanner = value;
            OnPropertyChangedWithValue(value);
        }
    }

    [DataSourceProperty]
    public ImageIdentifierVM? EnemyBanner
    {
        get
        {
            return _enemyBanner;
        }
        set
        {
            if (value == _enemyBanner)
            {
                return;
            }

            _enemyBanner = value;
            OnPropertyChangedWithValue(value);
        }
    }

    [DataSourceProperty]
    public CrpgScoreboardEndOfBattleVM EndOfBattle
    {
        get
        {
            return _endOfBattle;
        }
        set
        {
            if (value != _endOfBattle)
            {
                _endOfBattle = value;
                OnPropertyChangedWithValue(value, "EndOfBattle");
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<StringPairItemWithActionVM> PlayerActionList
    {
        get
        {
            return _playerActionList;
        }
        set
        {
            if (value != _playerActionList)
            {
                _playerActionList = value;
                OnPropertyChangedWithValue(value, "PlayerActionList");
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<CrpgScoreboardSideVM> Sides
    {
        get
        {
            return _sides;
        }
        set
        {
            if (value != _sides)
            {
                _sides = value;
                OnPropertyChangedWithValue(value, "Sides");
            }
        }
    }

    [DataSourceProperty]
    public bool IsUpdateOver
    {
        get
        {
            return _isUpdateOver;
        }
        set
        {
            _isUpdateOver = value;
            OnPropertyChangedWithValue(value, "IsUpdateOver");
        }
    }

    [DataSourceProperty]
    public bool IsInitalizationOver
    {
        get
        {
            return _isInitalizationOver;
        }
        set
        {
            if (value != _isInitalizationOver)
            {
                _isInitalizationOver = value;
                OnPropertyChangedWithValue(value, "IsInitalizationOver");
            }
        }
    }

    [DataSourceProperty]
    public bool IsMouseEnabled
    {
        get
        {
            return _isMouseEnabled;
        }
        set
        {
            if (value != _isMouseEnabled)
            {
                _isMouseEnabled = value;
                OnPropertyChangedWithValue(value, "IsMouseEnabled");
            }
        }
    }

    [DataSourceProperty]
    public bool IsActive
    {
        get
        {
            return _isActive;
        }
        set
        {
            if (value != _isActive)
            {
                _isActive = value;
                OnPropertyChangedWithValue(value, "IsActive");
            }
        }
    }

    [DataSourceProperty]
    public bool IsPlayerActionsActive
    {
        get
        {
            return _isPlayerActionsActive;
        }
        set
        {
            if (value != _isPlayerActionsActive)
            {
                _isPlayerActionsActive = value;
                OnPropertyChangedWithValue(value, "IsPlayerActionsActive");
            }
        }
    }

    [DataSourceProperty]
    public string Spectators
    {
        get
        {
            return _spectators;
        }
        set
        {
            if (value != _spectators)
            {
                _spectators = value;
                OnPropertyChangedWithValue(value, "Spectators");
            }
        }
    }

    [DataSourceProperty]
    public InputKeyItemVM ShowMouseKey
    {
        get
        {
            return _showMouseKey;
        }
        set
        {
            if (value != _showMouseKey)
            {
                _showMouseKey = value;
                OnPropertyChangedWithValue(value, "ShowMouseKey");
            }
        }
    }

    [DataSourceProperty]
    public string MissionName
    {
        get
        {
            return _missionName;
        }
        set
        {
            if (value != _missionName)
            {
                _missionName = value;
                OnPropertyChangedWithValue(value, "MissionName");
            }
        }
    }

    [DataSourceProperty]
    public string GameModeText
    {
        get
        {
            return _gameModeText;
        }
        set
        {
            if (value != _gameModeText)
            {
                _gameModeText = value;
                OnPropertyChangedWithValue(value, "GameModeText");
            }
        }
    }

    [DataSourceProperty]
    public string MapName
    {
        get
        {
            return _mapName;
        }
        set
        {
            if (value != _mapName)
            {
                _mapName = value;
                OnPropertyChangedWithValue(value, "MapName");
            }
        }
    }

    [DataSourceProperty]
    public string ServerName
    {
        get
        {
            return _serverName;
        }
        set
        {
            if (value != _serverName)
            {
                _serverName = value;
                OnPropertyChangedWithValue(value, "ServerName");
            }
        }
    }

    [DataSourceProperty]
    public bool IsBotsEnabled
    {
        get
        {
            return _isBotsEnabled;
        }
        set
        {
            if (value != _isBotsEnabled)
            {
                _isBotsEnabled = value;
                OnPropertyChangedWithValue(value, "IsBotsEnabled");
            }
        }
    }

    [DataSourceProperty]
    public bool IsSingleSide
    {
        get
        {
            return _isSingleSide;
        }
        set
        {
            if (value != _isSingleSide)
            {
                _isSingleSide = value;
                OnPropertyChangedWithValue(value, "IsSingleSide");
            }
        }
    }

    [DataSourceProperty]
    public string ToggleMuteText
    {
        get
        {
            return _toggleMuteText;
        }
        set
        {
            if (value != _toggleMuteText)
            {
                _toggleMuteText = value;
                OnPropertyChangedWithValue(value, "ToggleMuteText");
            }
        }
    }
}
