﻿using Crpg.Domain.Common;
using Crpg.Domain.Entities.Items;
using Crpg.Domain.Entities.Users;

namespace Crpg.Domain.Entities.Clans;

/// <summary>
/// Represents a clan membership for a user.
/// </summary>
public class ClanMember : AuditableEntity
{
    public int UserId { get; set; }
    public int ClanId { get; set; }
    public ClanMemberRole Role { get; set; }

    public User? User { get; set; }
    public Clan? Clan { get; set; }

    public IList<ClanArmoryItem> ArmoryItems { get; set; } = new List<ClanArmoryItem>();
    public IList<ClanArmoryBorrowedItem> ArmoryBorrowedItems { get; set; } = new List<ClanArmoryBorrowedItem>();
}
