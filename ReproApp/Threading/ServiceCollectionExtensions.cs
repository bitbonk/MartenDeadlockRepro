using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ReproApp.Threading;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add an <see cref="IHostedService" /> registration for the given type.
    /// </summary>
    /// <typeparam name="THostedService">An <see cref="T:Microsoft.Extensions.Hosting.IHostedService" /> to register.</typeparam>
    /// <param name="services">
    ///     The <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /> to register with.
    /// </param>
    /// <param name="useDedicatedSynchronizationContext">
    ///     Determines whether the hosted service will be created and started with its own
    ///     synchronization context.
    ///     If set to <see langword="true" />, <see cref="SynchronizationContext.Current" /> will be present in the constructor
    ///     of <typeparamref name="THostedService" /> as well as in <see cref="IHostedService.StartAsync" /> and
    ///     <see cref="IHostedService.StopAsync" />
    /// </param>
    /// <returns>The original <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" />.</returns>
    public static IServiceCollection AddHostedServiceWithSyncContext<THostedService>(
        this IServiceCollection services,
        bool useDedicatedSynchronizationContext)
        where THostedService : class, IHostedService
    {
        if (useDedicatedSynchronizationContext)
        {
            services.AddSingleton<IHostedService>(
                s => ActivatorUtilities.CreateInstance<SyncContextHostedServiceDecorator<THostedService>>(s));
        }
        else
        {
            services.AddHostedService<THostedService>();
        }

        return services;
    }

    /// <summary>
    ///     Gets the awaiter to switch to the <see cref="SynchronizationContext" />.
    /// </summary>
    /// <param name="context">The synchronization context to be extended.</param>
    /// <returns>The awaitable wrapper for the synchronization context.</returns>
    public static IAwaitable GetAwaiter(this SynchronizationContext context)
    {
        return new SynchronizationContextAwaiter(context);
    }

    /// <summary>
    ///     The implementation of <see cref="IAwaitable" /> to make a <see cref="SynchronizationContext" /> awaitable.
    /// </summary>
    internal struct SynchronizationContextAwaiter : IAwaitable
    {
        private static readonly SendOrPostCallback PostCallback = state =>
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            ((Action)state)();
        };

        private readonly SynchronizationContext context;
        private Exception? exception;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SynchronizationContextAwaiter" /> class.
        /// </summary>
        /// <param name="context">The context to be wrapped.</param>
        public SynchronizationContextAwaiter(SynchronizationContext context)
        {
            this.context = context;
            this.exception = null;
        }

        public bool IsCompleted => this.context == SynchronizationContext.Current;

        public void OnCompleted(Action continuation)
        {
            try
            {
                this.context.Post(PostCallback, continuation);
            }
            catch (Exception e)
            {
                this.exception = e;
                continuation();
            }
        }

        public void GetResult()
        {
            if (this.exception != null)
            {
                throw new InvalidOperationException("The action could not be posted.", this.exception);
            }
        }
    }

    private class SyncContextHostedServiceDecorator<TAdaptedHostedService> : IHostedService, IAsyncDisposable
        where TAdaptedHostedService : class, IHostedService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly SingleThreadSynchronizationContext synchronizationContext;
        private readonly ILogger logger;
        private TAdaptedHostedService? decoratedService;

        public SyncContextHostedServiceDecorator(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<TAdaptedHostedService>();
            this.synchronizationContext =
                new SingleThreadSynchronizationContext(TimeSpan.FromSeconds(10));
            this.synchronizationContext.StartThread("Foo");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.LogInformation("Starting hosted service");
            await this.synchronizationContext;
            this.decoratedService = ActivatorUtilities.CreateInstance<TAdaptedHostedService>(this.serviceProvider);
            await this.decoratedService.StartAsync(cancellationToken);
            this.logger.LogInformation("Started hosted service");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (this.decoratedService == null)
            {
                return;
            }

            this.logger.LogInformation("Stopping hosted service");
            await this.synchronizationContext;
            await this.decoratedService.StopAsync(cancellationToken);
            this.logger.LogInformation("Stopped hosted service");
        }

        public async ValueTask DisposeAsync()
        {
            await this.synchronizationContext.DisposeAsync().ConfigureAwait(false);
        }
    }
}