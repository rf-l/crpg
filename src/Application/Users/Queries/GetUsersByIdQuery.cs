﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using Crpg.Application.Common.Interfaces;
using Crpg.Application.Common.Mediator;
using Crpg.Application.Common.Results;
using Crpg.Application.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Crpg.Application.Users.Queries;

public record GetUsersByIdQuery : IMediatorRequest<IList<UserPrivateViewModel>>
{
    public int[] UserIds { get; init; } = Array.Empty<int>();

    internal class Handler : IMediatorRequestHandler<GetUsersByIdQuery, IList<UserPrivateViewModel>>
    {
        private readonly ICrpgDbContext _db;
        private readonly IMapper _mapper;

        public Handler(ICrpgDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        public async Task<Result<IList<UserPrivateViewModel>>> Handle(GetUsersByIdQuery req, CancellationToken cancellationToken)
        {
            var users = await _db.Users
                .ProjectTo<UserPrivateViewModel>(_mapper.ConfigurationProvider)
                .Where(u => req.UserIds.Contains(u.Id))
                .ToArrayAsync(cancellationToken);
            return new(users);
        }
    }
}
