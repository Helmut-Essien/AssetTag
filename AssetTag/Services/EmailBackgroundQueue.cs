using System.Threading.Channels;

namespace AssetTag.Services
{
    /// <summary>
    /// Fire-and-forget email work that runs outside the HTTP request scope
    /// (avoids scoped-service capture after dispose and SMTP timing side channels).
    /// </summary>
    public interface IEmailBackgroundQueue
    {
        ValueTask EnqueueAsync(Func<IEmailService, CancellationToken, Task> work);
    }

    public sealed class EmailBackgroundQueue : BackgroundService, IEmailBackgroundQueue
    {
        private readonly Channel<Func<IEmailService, CancellationToken, Task>> _channel =
            Channel.CreateUnbounded<Func<IEmailService, CancellationToken, Task>>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailBackgroundQueue> _logger;

        public EmailBackgroundQueue(
            IServiceScopeFactory scopeFactory,
            ILogger<EmailBackgroundQueue> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public ValueTask EnqueueAsync(Func<IEmailService, CancellationToken, Task> work)
        {
            if (work is null)
                throw new ArgumentNullException(nameof(work));

            return _channel.Writer.WriteAsync(work);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var work in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await work(emailService, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background email work failed");
                }
            }
        }
    }
}
