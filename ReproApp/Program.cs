using ReproApp;
using ReproApp.Threading;
using Microsoft.Extensions.Hosting;

await Host.CreateDefaultBuilder().ConfigureServices(
    collection =>
    {
        collection.AddHostedServiceWithSyncContext<FooService>(true);
    })
    .Build()
    .RunAsync();