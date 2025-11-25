using SigmabotSync.Domain.Entities;
using SigmabotSync.Domain.Interfaces;
using SigmabotSync.Infrastructure.External;
using SigmabotSync.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmabotSync.Application.Services
{
    public class ProjectService
    {
        private readonly SettingsService _settings;

        public ProjectService(SettingsService settings)
        {
            _settings = settings;
        }

        public async Task<List<Project>> GetProjectsAsync()
        {
            var cfg = _settings.Load();

            var client = new AconexProjectClient(
                cfg.UserAconex,
                cfg.PassAconex,
                cfg.IntegrationIdAconex
            );

            return await client.GetUserProjectsAsync();
        }
    }

}
