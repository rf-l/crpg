﻿using Crpg.Application.Clans.Models;
using Crpg.Application.Common.Mappings;
using Crpg.Domain.Entities.Items;

namespace Crpg.Application.Items.Models;
public class ClanArmoryItemViewModel : IMapFrom<ClanArmoryItem>
{
    public UserItemViewModel? UserItem { get; set; }
    public ClanArmoryBorrowedItemViewModel? BorrowedItem { get; set; }
    public DateTime UpdatedAt { get; set; }
}
