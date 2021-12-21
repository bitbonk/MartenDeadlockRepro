using System.Runtime.CompilerServices;

namespace ReproApp.Threading;

/// <summary>
///     The abstraction for classes to be awaitable.
/// </summary>
/// <remarks>
///     The following conditions needs to be fulfilled for a class to be awaitable:
///     <list type="bullet">
///         <item>
///             have a GetAwaiter() method (which can be an extension method), which returns an object that:
///             implements the INotifyCompletion interface
///         </item>
///         <item>
///             has an IsCompleted boolean property
///         </item>
///         <item>
///             has a GetResult() method that synchronously returns the result
///             (or void if there is no result)
///         </item>
///     </list>
///     <see href="https://www.thomaslevesque.com/2015/11/11/explicitly-switch-to-the-ui-thread-in-an-async-method/" />
/// </remarks>
public interface IAwaitable : INotifyCompletion
{
    /// <summary>
    ///     Gets a value whether awaiting operation is completed.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    ///     Method is called from framework to synchronously complete the asynchronous operation.
    /// </summary>
    void GetResult();
}