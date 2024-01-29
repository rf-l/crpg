﻿namespace Crpg.Domain.Entities.ActivityLogs;

public enum ActivityLogType
{
    UserCreated,
    UserDeleted,
    UserRenamed,
    UserRewarded,
    ItemBought,
    ItemSold,
    ItemBroke,
    ItemReforged,
    ItemRepaired,
    ItemUpgraded,
    CharacterCreated,
    CharacterDeleted,
    CharacterRatingReset,
    CharacterRespecialized,
    CharacterRetired,
    CharacterRewarded,
    CharacterEarned,
    ServerJoined,
    ChatMessageSent,
    TeamHit,
    ClanArmoryAddItem,
    ClanArmoryRemoveItem,
    ClanArmoryReturnItem,
    ClanArmoryBorrowItem,
}
