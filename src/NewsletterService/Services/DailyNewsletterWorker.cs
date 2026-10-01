namespace NewsletterService.Services;

public class DailyNewsletterWorker : BackgroundService
{
    private readonly DailyNewsletterSender _sender;
    private readonly ILogger<DailyNewsletterWorker> _logger;
    private readonly TimeSpan _interval;

    public DailyNewsletterWorker(
        DailyNewsletterSender sender,
        ILogger<DailyNewsletterWorker> logger,
        IConfiguration configuration)
    {
        _sender = sender;
        _logger = logger;

        _interval = configuration.GetValue<TimeSpan?>(
            "DailyNewsletter:Interval") ?? TimeSpan.FromDays(1);
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        _logger.LogInformation(
            "Daily newsletter timer started with interval {Interval}",
            _interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _sender.SendAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Scheduled daily newsletter failed.");
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }
}