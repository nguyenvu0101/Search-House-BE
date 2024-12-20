﻿using TD.KCN.WebApi.Infrastructure.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace TD.KCN.WebApi.Infrastructure.BackgroundJobs.RecurringJobs.Initialization;

internal static class Startup
{
    internal static IServiceCollection AddRecurringBackgroundJobs(this IServiceCollection services)
    {
        services.AddServices(typeof(IRecurringJobInitialization), ServiceLifetime.Transient);

        return services;
    }
}