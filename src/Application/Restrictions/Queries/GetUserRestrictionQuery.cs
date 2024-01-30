﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using Crpg.Application.Common.Interfaces;
using Crpg.Application.Common.Mediator;
using Crpg.Application.Common.Results;
using Crpg.Application.Restrictions.Models;
using Crpg.Domain.Entities.Restrictions;
using Crpg.Sdk.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Crpg.Application.Restrictions.Queries;

public record GetUserRestrictionQuery : IMediatorRequest<RestrictionPublicViewModel>
{
    public int UserId { get; init; }

    internal class Handler : IMediatorRequestHandler<GetUserRestrictionQuery, RestrictionPublicViewModel>
    {
        private readonly ICrpgDbContext _db;
        private readonly IMapper _mapper;
        private readonly IDateTime _dateTime;

        public Handler(ICrpgDbContext db, IMapper mapper, IDateTime dateTime)
        {
            _db = db;
            _mapper = mapper;
            _dateTime = dateTime;
        }

        public async Task<Result<RestrictionPublicViewModel>> Handle(GetUserRestrictionQuery req, CancellationToken cancellationToken)
        {
            var lastJoinOrAllRestriction = await _db.Restrictions
                .Where(r =>
                    r.RestrictedUserId == req.UserId
                    && (r.Type == RestrictionType.Join || r.Type == RestrictionType.All)
                    && _dateTime.UtcNow < r.CreatedAt + r.Duration)
                .OrderByDescending(r => r.CreatedAt)
                .ProjectTo<RestrictionPublicViewModel>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(cancellationToken);

            return new(lastJoinOrAllRestriction);
        }
    }
}
