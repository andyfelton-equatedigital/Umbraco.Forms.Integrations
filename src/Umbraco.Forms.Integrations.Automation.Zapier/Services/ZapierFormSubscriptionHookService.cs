﻿using Microsoft.Extensions.Logging;

using System.Collections.Generic;
using System.Linq;

using Umbraco.Forms.Integrations.Automation.Zapier.Models.Dtos;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Umbraco.Forms.Integrations.Automation.Zapier.Services
{
    public class ZapierFormSubscriptionHookService
    {
        private readonly IScopeProvider _scopeProvider;

        private readonly ILogger<ZapierFormSubscriptionHookService> _logger;

        public ZapierFormSubscriptionHookService(IScopeProvider scopeProvider, ILogger<ZapierFormSubscriptionHookService> logger)
        {
            _scopeProvider = scopeProvider;

            _logger = logger;
        }

        public bool TryGetById(string id, out IEnumerable<SubscriptionDto> dto)
        {
            using (var scope = _scopeProvider.CreateScope())
            {
                var entities = scope.Database
                    .Query<SubscriptionDto>("SELECT * FROM zapierSubscriptionHook where Type=@0", Constants.EntityType.Form)
                    .Where(p => p.EntityId == id)
                    .ToList();

                dto = entities;

                return entities.Any();
            }
        }
    }
}
