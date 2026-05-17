using Microsoft.AspNetCore.Mvc;
using ChecaAI.Application.Interfaces;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MunicipalController : ControllerBase
{
    private readonly IWebScrapingService _webScrapingService;

    public MunicipalController(IWebScrapingService webScrapingService)
    {
        _webScrapingService = webScrapingService;
    }

    [HttpGet("cities/{cityName}/councilors")]
    public async Task<ActionResult<IEnumerable<Politician>>> GetCouncilors(
        string cityName, 
        [FromQuery] string stateCode)
    {
        if (string.IsNullOrEmpty(stateCode))
        {
            return BadRequest("State code is required");
        }

        if (!await _webScrapingService.IsCitySupportedAsync(cityName, stateCode))
        {
            return NotFound($"City {cityName}-{stateCode} is not supported for web scraping yet");
        }

        var councilors = await _webScrapingService.GetCouncilorsFromCityAsync(cityName, stateCode);
        return Ok(councilors);
    }

    [HttpGet("cities/{cityName}/voting-sessions")]
    public async Task<ActionResult<IEnumerable<VotingSession>>> GetVotingSessions(
        string cityName, 
        [FromQuery] string stateCode)
    {
        if (string.IsNullOrEmpty(stateCode))
        {
            return BadRequest("State code is required");
        }

        if (!await _webScrapingService.IsCitySupportedAsync(cityName, stateCode))
        {
            return NotFound($"City {cityName}-{stateCode} is not supported for web scraping yet");
        }

        var sessions = await _webScrapingService.GetVotingSessionsFromCityAsync(cityName, stateCode);
        return Ok(sessions);
    }

    [HttpGet("cities/{cityName}/proposals")]
    public async Task<ActionResult<IEnumerable<Proposal>>> GetProposals(
        string cityName, 
        [FromQuery] string stateCode)
    {
        if (string.IsNullOrEmpty(stateCode))
        {
            return BadRequest("State code is required");
        }

        if (!await _webScrapingService.IsCitySupportedAsync(cityName, stateCode))
        {
            return NotFound($"City {cityName}-{stateCode} is not supported for web scraping yet");
        }

        var proposals = await _webScrapingService.GetProposalsFromCityAsync(cityName, stateCode);
        return Ok(proposals);
    }

    [HttpGet("cities/{cityName}/supported")]
    public async Task<ActionResult<object>> CheckCitySupport(
        string cityName, 
        [FromQuery] string stateCode)
    {
        if (string.IsNullOrEmpty(stateCode))
        {
            return BadRequest("State code is required");
        }

        var isSupported = await _webScrapingService.IsCitySupportedAsync(cityName, stateCode);
        
        return Ok(new
        {
            City = cityName,
            State = stateCode,
            IsSupported = isSupported,
            Message = isSupported 
                ? $"City {cityName}-{stateCode} is supported for web scraping"
                : $"City {cityName}-{stateCode} is not supported yet. Web scraping configuration needed."
        });
    }

    [HttpGet("supported-cities")]
    public ActionResult<object> GetSupportedCities()
    {
        var supportedCities = new[]
        {
            new { City = "São Paulo", State = "SP", Features = new[] { "Councilors", "Proposals", "Sessions" } },
            new { City = "Rio de Janeiro", State = "RJ", Features = new[] { "Councilors", "Proposals", "Sessions" } },
            new { City = "Belo Horizonte", State = "MG", Features = new[] { "Councilors", "Proposals", "Sessions" } }
        };

        return Ok(new
        {
            Message = "Currently supported cities for web scraping",
            Count = supportedCities.Length,
            Cities = supportedCities,
            Note = "Each city requires specific configuration based on their municipal chamber website structure"
        });
    }
}