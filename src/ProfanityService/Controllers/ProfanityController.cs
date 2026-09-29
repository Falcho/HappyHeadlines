using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProfanityService.Contracts;
using ProfanityService.Data;

namespace ProfanityService.Controllers;

[ApiController]
[Route("profanity")]
public class ProfanityController : ControllerBase
{
    private readonly ProfanityDbContext _database;

    public ProfanityController(ProfanityDbContext database)
    {
        _database = database;
    }

    [HttpPost("check")]
    public async Task<IActionResult> Check(CheckTextRequest request)
    {
        var prohibitedWords = await _database.ProhibitedWords
            .Select(word => word.Word)
            .ToListAsync();

        var submittedWords = Regex.Matches(request.Text, @"\p{L}+")
            .Select(match => match.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var containsProfanity =
            prohibitedWords.Any(word => submittedWords.Contains(word));

        return Ok(new { containsProfanity });
    }
}