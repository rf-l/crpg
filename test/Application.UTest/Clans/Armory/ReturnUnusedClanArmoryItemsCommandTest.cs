﻿using Crpg.Application.Clans.Commands.Armory;
using Crpg.Sdk;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Crpg.Application.UTest.Clans.Armory;
public class ReturnUnusedClanArmoryItemsCommandTest : TestBase
{
    [TestCase(3, 0)]
    [TestCase(11, 4)]
    public async Task Basic(int clanArmoryTimeoutDays, int clanArmoryBorrowedItemsCount)
    {
        await ClanArmoryTestHelper.CommonSetUp(ArrangeDb, armoryTimeout: clanArmoryTimeoutDays);
        await ClanArmoryTestHelper.AddItems(ArrangeDb, "user0", 2);
        await ClanArmoryTestHelper.AddItems(ArrangeDb, "user1", 2);
        await ClanArmoryTestHelper.BorrowItems(ArrangeDb, "user2", 2);
        await ClanArmoryTestHelper.BorrowItems(ArrangeDb, "user3", 2);
        await ArrangeDb.Users.ForEachAsync(u => u.UpdatedAt = DateTime.UtcNow.Subtract(TimeSpan.FromDays(10)));
        await ArrangeDb.SaveChangesAsync();

        Assert.That(ActDb.ClanArmoryBorrowedItems.Count(), Is.EqualTo(4));

        var handler = new ReturnUnusedItemsToClanArmoryCommand.Handler(ActDb, new MachineDateTime());
        var result = await handler.Handle(new ReturnUnusedItemsToClanArmoryCommand(), CancellationToken.None);

        Assert.That(result.Errors, Is.Null);

        Assert.That(AssertDb.ClanArmoryBorrowedItems.Count(), Is.EqualTo(clanArmoryBorrowedItemsCount));
    }
}
