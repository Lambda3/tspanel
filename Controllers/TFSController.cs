﻿using Microsoft.AspNet.Mvc;
using Microsoft.Framework.OptionsModel;
using System.Linq;
using System.Threading.Tasks;
using TfsPanel.Configuration;
using TfsPanel.Models;
using TfsPanel.Vso;

namespace TfsPanel.Controllers
{
    [Route("api")]
    public class TFSController : Controller
    {
        private readonly IOptions<AppSettings> configuration;
        private readonly VsoFactory factory;

        public TFSController(VsoFactory factory, IOptions<AppSettings> configuration)
        {
            this.factory = factory;
            this.configuration = configuration;
        }

        [HttpGet, Route("/api/builds")]
        public async Task<JsonResult> Builds()
        {
            var server = factory.CreateBuildServer();
            var definitions = await server.BuildDefinitions();

            var buildsByDefs = (await server.Builds(definitions, 1))
                .OrderByDescending(build => build.FinishTime)
                .GroupBy(build => build.Definition);

            var lastExecutions = buildsByDefs
                .Where(group => group.Any())
                .Select(group => group.First())
                .OrderByDescending(build => build.Status)
                .ThenByDescending(build => build.FinishTime)
                .Take(configuration.Options.MaxItems);

            var previousExecutions = Enumerable.Empty<Build>();
            if (lastExecutions.Count() < configuration.Options.MaxItems)
                previousExecutions = buildsByDefs
                    .SelectMany(group => group.Skip(1))
                    .OrderByDescending(build => build.FinishTime);


            var builds = lastExecutions
                .Concat(previousExecutions)
                .Take(configuration.Options.MaxItems)
                .Select(build => new
                {
                    number = build.Number,
                    name = build.Definition,
                    author = build.Author,
                    status = build.Status.ToJson(),
                    date = build.FinishTime.ToString("dd/MM/yyy HH:mm"),
                    duration = build.Duration
                });

            return Json(builds);
        }

        [HttpGet, Route("/api/pullrequests")]
        public async Task<JsonResult> PullRequests()
        {
            var server = factory.CreatePullRequestsServer();

            var repos = await server.Repositories();
            var prs = (await server.ActivePullRequests(repos))
                .OrderByDescending(pr => pr.CreationDate)
                .Take(configuration.Options.MaxItems)
                .Select(pr => new
                {
                    from = pr.FromBranch,
                    author = pr.Author,
                    to = pr.ToBranch,
                    title = pr.Title,
                    repo = pr.Repo.Name,
                    date = pr.CreationDate.ToString("dd/MM/yyy hh:mm")
                });

            return Json(prs);
        }
    }
}