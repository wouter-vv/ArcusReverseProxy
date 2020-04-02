using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GuardNet;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Arcus.Demo.WebAPI.Controllers
{
    [ApiController]
    [Route("/")]
    public class StartupController : Controller
    {
        private readonly HealthCheckService _healthCheckService;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthController"/> class.
        /// </summary>
        /// <param name="healthCheckService">The service to provide the health of the API application.</param>
        public StartupController(HealthCheckService healthCheckService)
        {
            Guard.NotNull(healthCheckService, nameof(healthCheckService));

            _healthCheckService = healthCheckService;
        }

        [HttpGet(Name = "Health_Get_Startup")]
        [ProducesResponseType(typeof(HealthReport), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(HealthReport), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Get()
        {
            HealthReport healthReport = await _healthCheckService.CheckHealthAsync();

            if (healthReport?.Status == HealthStatus.Healthy)
            {
                return Ok(healthReport);
            }
            else
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, healthReport);
            }
        }
    }
}