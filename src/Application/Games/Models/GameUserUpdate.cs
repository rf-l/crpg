﻿using Crpg.Application.Characters.Models;

namespace Crpg.Application.Games.Models;

public record GameUserUpdate
{
    public int UserId { get; init; }
    public int CharacterId { get; init; }
    public GameUserReward Reward { get; init; } = new();
    public CharacterStatisticsViewModel Statistics { get; init; } = new();
    public CharacterRatingViewModel Rating { get; init; } = new();
    public IList<GameUserDamagedItem> BrokenItems { get; init; } = Array.Empty<GameUserDamagedItem>();
    public string Instance { get; init; } = string.Empty;
}
