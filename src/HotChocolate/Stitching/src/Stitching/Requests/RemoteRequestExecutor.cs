using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GreenDonut;
using HotChocolate.Execution;

namespace HotChocolate.Stitching.Requests;

internal sealed class RemoteRequestExecutor
    : IRemoteRequestExecutor
        , IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly List<BufferedRequest> _bufferedRequests = new();
    private readonly IBatchScheduler _batchScheduler;
    private readonly IRequestExecutor _executor;
    private bool _taskRegistered;

    public RemoteRequestExecutor(
        IBatchScheduler batchScheduler,
        IRequestExecutor executor)
    {
        _batchScheduler = batchScheduler ??
            throw new ArgumentNullException(nameof(batchScheduler));
        _executor = executor ??
            throw new ArgumentNullException(nameof(executor));
    }

    /// <inheritdoc />
    public ISchema Schema => _executor.Schema;

    /// <inheritdoc />
    public IServiceProvider Services => _executor.Services;

    /// <inheritdoc />
    public Task<IExecutionResult> ExecuteAsync(
        IQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var bufferRequest = BufferedRequest.Create(request, Schema);

        _semaphore.Wait(cancellationToken);

        try
        {
            _bufferedRequests.Add(bufferRequest);

            if (!_taskRegistered)
            {
                _batchScheduler.Schedule(() => ExecuteRequestsInternal(cancellationToken));
                _taskRegistered = true;
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return bufferRequest.Promise.Task;
    }

    private async ValueTask ExecuteRequestsInternal(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_bufferedRequests.Count == 1)
            {
                await ExecuteSingleRequestAsync(cancellationToken).ConfigureAwait(false);
            }

            if (_bufferedRequests.Count > 1)
            {
                await ExecuteBufferedRequestBatchAsync(cancellationToken).ConfigureAwait(false);
            }

            // reset the states so that we are ready for new requests to be buffered.
            _taskRegistered = false;
            _bufferedRequests.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async ValueTask ExecuteSingleRequestAsync(
        CancellationToken cancellationToken)
    {
        var request = _bufferedRequests[0];

        var result = await _executor
            .ExecuteAsync(request.Request, cancellationToken)
            .ConfigureAwait(false);

        if (result is IQueryResult queryResult)
        {
            request.Promise.SetResult(queryResult);
        }
        else
        {
            // since we only support query/mutation at this point we will just fail
            // in the event that something else was returned.
            request.Promise.SetException(new NotSupportedException(
                "Only IQueryResult is supported when batching."));
        }
    }

    private async ValueTask ExecuteBufferedRequestBatchAsync(
        CancellationToken cancellationToken)
    {
        // first we take all buffered requests and merge them into a single request.
        // we however have to group requests by operation type. This means we should
        // end up with one or two requests (query and mutation).
        foreach ((IQueryRequest Merged, IEnumerable<BufferedRequest> Requests) batch in
            MergeRequestHelper.MergeRequests(_bufferedRequests))
        {
            // now we take this merged request and run it against the executor.
            var result = await _executor
                .ExecuteAsync(batch.Merged, cancellationToken)
                .ConfigureAwait(false);

            if (result is IQueryResult queryResult)
            {
                // last we will extract the results for the original buffered requests
                // and fulfil the promises.
                MergeRequestHelper.DispatchResults(queryResult, batch.Requests);
            }
            else
            {
                // since we only support query/mutation at this point we will just fail
                // in the event that something else was returned.
                foreach (var request in batch.Requests)
                {
                    request.Promise.SetException(new NotSupportedException(
                        "Only IQueryResult is supported when batching."));
                }
            }
        }
    }

    public void Dispose()
    {
        _semaphore.Dispose();

        if (_executor is IDisposable d)
        {
            d.Dispose();
        }
    }
}
