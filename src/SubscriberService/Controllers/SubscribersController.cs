using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubscriberService.Contracts;
using SubscriberService.Data;
using SubscriberService.Messaging;
using SubscriberService.Models;

namespace SubscriberService.Controllers;

[ApiController]
[Route("subscribers")]
public class SubscribersController : ControllerBase
{
    private readonly SubscriberDbContext _database;
    private readonly ILogger<SubscribersController> _logger;
    private readonly SubscriberQueuePublisher _queuePublisher;

    public SubscribersController(
        SubscriberDbContext database,
        ILogger<SubscribersController> logger,
        SubscriberQueuePublisher queuePublisher)
    {
        _database = database;
        _logger = logger;
        _queuePublisher = queuePublisher;
    }

    [HttpPost]
    public async Task<ActionResult<Subscriber>> Subscribe(
        SubscribeRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _database.Subscribers.AnyAsync(
                subscriber => subscriber.Email == email))
        {
            return Conflict("The email is already subscribed.");
        }

        var subscriber = new Subscriber
        {
            Id = Guid.NewGuid(),
            Email = email,
            SubscribedAt = DateTimeOffset.UtcNow
        };

        _database.Subscribers.Add(subscriber);
        await _database.SaveChangesAsync();

        await _queuePublisher.PublishAsync(new SubscriberCreated
        {
            Id = subscriber.Id,
            Email = subscriber.Email,
            SubscribedAt = subscriber.SubscribedAt
        });

        _logger.LogInformation(
            "Subscriber {SubscriberId} subscribed", subscriber.Id);

        return StatusCode(StatusCodes.Status201Created, subscriber);
    }

    [HttpDelete("{email}")]
    public async Task<IActionResult> Unsubscribe(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var subscriber = await _database.Subscribers
            .FirstOrDefaultAsync(
                subscriber => subscriber.Email == normalizedEmail);

        if (subscriber is null)
        {
            return NotFound();
        }

        _database.Subscribers.Remove(subscriber);
        await _database.SaveChangesAsync();

        _logger.LogInformation(
            "Subscriber {SubscriberId} unsubscribed", subscriber.Id);

        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<List<Subscriber>>> GetAll()
    {
        var subscribers = await _database.Subscribers
            .AsNoTracking()
            .OrderBy(subscriber => subscriber.SubscribedAt)
            .ToListAsync();

        return Ok(subscribers);
    }
}