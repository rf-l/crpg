using AutoMapper;
using AutoMapper.QueryableExtensions;
using Crpg.Application.Common.Interfaces;
using Crpg.Application.Common.Mediator;
using Crpg.Application.Common.Results;
using Crpg.Application.Common.Services;
using Crpg.Application.Items.Models;
using Microsoft.EntityFrameworkCore;

namespace Crpg.Application.Clans.Queries;

public record GetClanArmoryQuery : IMediatorRequest<IList<ClanArmoryItemViewModel>>
{
    public int UserId { get; init; }
    public int ClanId { get; init; }

    internal class Handler : IMediatorRequestHandler<GetClanArmoryQuery, IList<ClanArmoryItemViewModel>>
    {
        private readonly ICrpgDbContext _db;
        private readonly IMapper _mapper;
        private readonly IClanService _clanService;

        public Handler(ICrpgDbContext db, IMapper mapper, IClanService clanService)
        {
            _db = db;
            _mapper = mapper;
            _clanService = clanService;
        }

        public async Task<Result<IList<ClanArmoryItemViewModel>>> Handle(GetClanArmoryQuery req, CancellationToken cancellationToken)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == req.UserId)
                .Include(u => u.ClanMembership)
                .FirstOrDefaultAsync(cancellationToken);
            if (user == null)
            {
                return new(CommonErrors.UserNotFound(req.UserId));
            }

            var error = _clanService.CheckClanMembership(user, req.ClanId);
            if (error != null)
            {
                return new(error);
            }

            var items = await _db.ClanArmoryItems
                .AsNoTracking()
                .AsSplitQuery()
                .Where(ci => ci.LenderClanId == req.ClanId)
                .Include(ci => ci.BorrowedItem)
                .Include(ci => ci.UserItem!)
                    .ThenInclude(ui => ui.Item)
                .ToListAsync(cancellationToken);

            return new(_mapper.Map<IList<ClanArmoryItemViewModel>>(items));
        }
    }
}
