using Crpg.Application.Common;
using Crpg.Application.Common.Interfaces;
using Crpg.Application.Common.Mediator;
using Crpg.Application.Common.Results;
using Crpg.Application.Common.Services;
using Crpg.Domain.Entities.Restrictions;
using Crpg.Sdk.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LoggerFactory = Crpg.Logging.LoggerFactory;

namespace Crpg.Application.Users.Commands;

/// <summary>
/// Deletes all entities related to user except <see cref="Restriction"/>s and reset user info.
/// </summary>
public record DeleteUserCommand : IMediatorRequest
{
    public int UserId { get; init; }

    internal class Handler : IMediatorRequestHandler<DeleteUserCommand>
    {
        private static readonly ILogger Logger = LoggerFactory.CreateLogger<DeleteUserCommand>();

        private readonly ICrpgDbContext _db;
        private readonly IDateTime _dateTime;
        private readonly IUserService _userService;
        private readonly IActivityLogService _activityLogService;

        public Handler(ICrpgDbContext db, IDateTime dateTime, IUserService userService, IActivityLogService activityLogService)
        {
            _db = db;
            _dateTime = dateTime;
            _userService = userService;
            _activityLogService = activityLogService;
        }

        public async Task<Result> Handle(DeleteUserCommand req, CancellationToken cancellationToken)
        {
            var user = await _db.Users
                .Include(u => u.Characters)
                .Include(u => u.Items)
                .Include(u => u.Party!).ThenInclude(h => h.Items)
                .FirstOrDefaultAsync(u => u.Id == req.UserId, cancellationToken);
            if (user == null)
            {
                return new Result(CommonErrors.UserNotFound(req.UserId));
            }

            string name = user.Name;

            _userService.SetDefaultValuesForUser(user);
            user.Name = string.Empty;
            user.Avatar = new Uri("https://via.placeholder.com/184x184");
            user.DeletedAt = _dateTime.UtcNow; // Deleted users are just marked with a DeletedAt != null

            await _db.EquippedItems
                .RemoveRangeAsync(ei => ei.UserItem!.UserId == req.UserId, cancellationToken);

            await _db.ClanArmoryBorrowedItems
                .RemoveRangeAsync(bi => bi.BorrowerUserId == req.UserId, cancellationToken);

            await _db.ClanArmoryItems
                .RemoveRangeAsync(ci => ci.LenderUserId == req.UserId, cancellationToken);

            _db.UserItems.RemoveRange(user.Items);
            _db.Characters.RemoveRange(user.Characters);
            if (user.Party != null)
            {
                _db.PartyItems.RemoveRange(user.Party!.Items);
                _db.Parties.Remove(user.Party);
            }

            _db.ActivityLogs.Add(_activityLogService.CreateUserDeletedLog(user.Id));

            await _db.SaveChangesAsync(cancellationToken);
            Logger.LogInformation("{0} left ({1}#{2})", name, user.Platform, user.PlatformUserId);
            return Result.NoErrors;
        }
    }
}
