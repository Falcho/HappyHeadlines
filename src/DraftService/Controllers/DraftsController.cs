using DraftService.Contracts;
using DraftService.Data;
using DraftService.Models;
using Microsoft.AspNetCore.Mvc;

namespace DraftService.Controllers;

[ApiController]
[Route("drafts")]
public class DraftsController : ControllerBase
{
    private readonly DraftDbContext _database;
    private readonly ILogger<DraftsController> _logger;

    public DraftsController(
        DraftDbContext database,
        ILogger<DraftsController> logger)
    {
        _database = database;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<Draft>> Create(
        SaveDraftRequest request)
    {
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _database.Drafts.Add(draft);
        await _database.SaveChangesAsync();

        _logger.LogInformation("Created draft {DraftId}", draft.Id);

        return CreatedAtAction(
            nameof(Read),
            new { id = draft.Id },
            draft);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Draft>> Read(Guid id)
    {
        var draft = await _database.Drafts.FindAsync(id);

        if (draft is null)
        {
            _logger.LogInformation("Draft {DraftId} was not found", id);
            return NotFound();
        }

        _logger.LogDebug("Retrieved draft {DraftId}", id);

        return Ok(draft);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Draft>> Update(
        Guid id,
        SaveDraftRequest request)
    {
        var draft = await _database.Drafts.FindAsync(id);

        if (draft is null)
        {
            _logger.LogInformation(
                "Cannot update missing draft {DraftId}", id);

            return NotFound();
        }

        draft.Title = request.Title;
        draft.Content = request.Content;
        draft.UpdatedAt = DateTimeOffset.UtcNow;

        await _database.SaveChangesAsync();

        _logger.LogInformation("Updated draft {DraftId}", id);

        return Ok(draft);
    }
}