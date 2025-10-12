// src/ThoughtGarden.Api/Controllers/HealthController.cs
using Microsoft.AspNetCore.Mvc;
using ThoughtGarden.Api.Infrastructure;

namespace ThoughtGarden.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IServerInfoProvider _provider;
    public HealthController(IServerInfoProvider provider) => _provider = provider;

    [HttpGet]
    public IActionResult Get() => Ok(_provider.Get());
}
