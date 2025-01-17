﻿using Ardalis.Specification;
using TD.KCN.WebApi.Application.Common.Specification;
using TD.KCN.WebApi.Application.Identity.Users;

namespace TD.KCN.WebApi.Infrastructure.Identity;

public class UserListPaginationFilterSpec : EntitiesByPaginationFilterSpec<ApplicationUser>
{
    public UserListPaginationFilterSpec(UserListFilter request)
        : base(request)
    {

    }

    public static DateTime GetDateZeroTime(DateTime date)
    {
        return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
    }
}