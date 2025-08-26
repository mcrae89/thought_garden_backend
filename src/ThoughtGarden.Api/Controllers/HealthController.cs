using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace ThoughtGarden.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        var timeZone = TimeZoneInfo.Local.DisplayName;

        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.Now,
            timeZone,
            version
        });
    }
}
