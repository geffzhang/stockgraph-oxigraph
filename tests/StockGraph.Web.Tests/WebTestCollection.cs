using Xunit;

namespace StockGraph.Web.Tests;

/// <summary>
/// A static semaphore that serializes Web integration test execution.
/// Even when xUnit parallelizes test starts, only one test can hold the lock at a time,
/// ensuring only one WebApplicationFactory/host/store is active concurrently.
/// </summary>
public static class WebTestLock
{
    public static SemaphoreSlim Instance { get; } = new(1, 1);
}
