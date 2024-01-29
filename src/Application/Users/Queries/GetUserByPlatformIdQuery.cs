﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using Crpg.Application.Common.Interfaces;
using Crpg.Application.Common.Mediator;
using Crpg.Application.Common.Results;
using Crpg.Application.Users.Models;
using Crpg.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace Crpg.Application.Users.Queries;

public record GetUserByPlatformIdQuery : IMediatorRequest<UserPrivateViewModel>
{
    public Platform Platform { get; init; }
    public string PlatformUserId { get; init; } = string.Empty;

    internal class Handler : IMediatorRequestHandler<GetUserByPlatformIdQuery, UserPrivateViewModel>
    {
        private readonly ICrpgDbContext _db;
        private readonly IMapper _mapper;

        public Handler(ICrpgDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        public async Task<Result<UserPrivateViewModel>> Handle(GetUserByPlatformIdQuery req, CancellationToken cancellationToken)
        {
            var user = await _db.Users
                .ProjectTo<UserPrivateViewModel>(_mapper.ConfigurationProvider)
                .Where(u => u.Platform == req.Platform && u.PlatformUserId == req.PlatformUserId)
                .FirstOrDefaultAsync(cancellationToken);
            return user == null
                ? new(CommonErrors.UserNotFound(req.Platform, req.PlatformUserId))
                : new(user);
        }
    }
}
