using Crpg.Application.Common.Interfaces;
using Crpg.Application.Common.Mediator;
using Crpg.Application.Common.Results;
using Crpg.Sdk.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LoggerFactory = Crpg.Logging.LoggerFactory;

namespace Crpg.Application.Clans.Commands.Armory;

public record ReturnUnusedItemsToClanArmoryCommand : IMediatorRequest
{
    internal class Handler : IMediatorRequestHandler<ReturnUnusedItemsToClanArmoryCommand>
    {
        private static readonly ILogger Logger = LoggerFactory.CreateLogger<ReturnUnusedItemsToClanArmoryCommand>();

        private readonly ICrpgDbContext _db;
        private readonly IDateTime _dateTime;

        public Handler(ICrpgDbContext db, IDateTime dateTime)
        {
            _db = db;
            _dateTime = dateTime;
        }

        public async Task<Result> Handle(ReturnUnusedItemsToClanArmoryCommand req, CancellationToken cancellationToken)
        {
            var now = _dateTime.UtcNow;
            var users = await _db.Users
                .AsSplitQuery()
                .Include(u => u.ClanMembership!)
                    .ThenInclude(cm => cm.ArmoryBorrowedItems)
                    .ThenInclude(bi => bi.UserItem!)
                    .ThenInclude(ui => ui.EquippedItems)
                .Where(u => u.ClanMembership != null && u.ClanMembership.ArmoryBorrowedItems.Count > 0 && (now - u.UpdatedAt) > u.ClanMembership.Clan!.ArmoryTimeout)
                .ToArrayAsync(cancellationToken);

            foreach (var u in users)
            {
                var equipped = u.ClanMembership!.ArmoryBorrowedItems.SelectMany(bi => bi.UserItem!.EquippedItems);
                _db.EquippedItems.RemoveRange(equipped);
                _db.ClanArmoryBorrowedItems.RemoveRange(u.ClanMembership!.ArmoryBorrowedItems);
            }

            await _db.SaveChangesAsync(cancellationToken);
            Logger.LogInformation("Return unused items");

            return Result.NoErrors;
        }
    }
}
