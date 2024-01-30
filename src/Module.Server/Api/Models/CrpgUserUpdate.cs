﻿using Crpg.Module.Api.Models.Characters;

namespace Crpg.Module.Api.Models;

// Copy of Crpg.Application.Games.Models.GameUserUpdate
internal class CrpgUserUpdate
{
    public int UserId { get; set; }
    public int CharacterId { get; set; }
    public CrpgUserReward? Reward { get; set; }
    public CrpgCharacterStatistics Statistics { get; set; } = default!;
    public CrpgCharacterRating Rating { get; set; } = default!;
    public IList<CrpgUserDamagedItem> BrokenItems { get; set; } = Array.Empty<CrpgUserDamagedItem>();
    public string Instance { get; set; } = string.Empty;
}
