namespace ReproApp;

using Marten;
using Microsoft.Extensions.Hosting;

public class FooService : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var store = DocumentStore.For("host=localhost;port=5433;database=repro;password=postgres;username=normal");

        await Read<Foo>(cancellationToken, store);
        
        // The following call never completes.
        // But reading the same table twice (call Read<Foo>() again) does not yield the problem.
        await Read<Bar>(cancellationToken, store); 

        await base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }

    private static async Task Read<T>(CancellationToken cancellationToken, DocumentStore store)
    {
        await using var session = store.QuerySession();
        var documents = await session
            .Query<T>()
            .ToListAsync(token: cancellationToken)
            .ConfigureAwait(false);
    }

    public class Foo
    {
        public int Id { get; set; }
        public string Text { get; set; }
    }

    public class Bar
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }
}
