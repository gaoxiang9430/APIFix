﻿using System.Diagnostics;
using System.Linq;
using System.Threading;
using Polly.Utilities;

namespace Polly.Caching
{
    /// <summary>
    /// A cache policy that can be applied to the results of delegate executions.
    /// </summary>
    public partial class CachePolicy : Policy, ICachePolicy
    {
        private readonly ISyncCacheProvider _syncCacheProvider;
        private readonly ITtlStrategy _ttlStrategy;
        private readonly Func<Context, string> _cacheKeyStrategy;

        private readonly Action<Context, string> _onCacheGet;
        private readonly Action<Context, string> _onCacheMiss;
        private readonly Action<Context, string> _onCachePut;
        private readonly Action<Context, string, Exception> _onCacheGetError;
        private readonly Action<Context, string, Exception> _onCachePutError;

public CachePolicy(){}

        public CachePolicy(
            ISyncCacheProvider syncCacheProvider, 
            ITtlStrategy ttlStrategy,
            Func<Context, string> cacheKeyStrategy,
            Action<Context, string> onCacheGet,
            Action<Context, string> onCacheMiss,
            Action<Context, string> onCachePut,
            Action<Context, string, Exception> onCacheGetError,
            Action<Context, string, Exception> onCachePutError)
            : base((action, context, cancellationToken) => action(context, cancellationToken), // Pass-through/NOOP policy action, for void-returning calls through a cache policy.
                PredicateHelper.EmptyExceptionPredicates)
        {
            _syncCacheProvider = syncCacheProvider;
            _ttlStrategy = ttlStrategy;
            _cacheKeyStrategy = cacheKeyStrategy;

            _onCacheGet = onCacheGet;
            _onCachePut = onCachePut;
            _onCacheMiss = onCacheMiss;
            _onCacheGetError = onCacheGetError;
            _onCachePutError = onCachePutError;
        }

        /// <summary>
        /// Executes the specified action within the cache policy and returns the result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="action">The action to perform.</param>
        /// <param name="context">Execution context that is passed to the exception policy; defines the cache key to use in cache lookup.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The value returned by the action, or the cache.</returns>
        [DebuggerStepThrough]
        override TResult ExecuteInternal<TResult>(Func<Context, CancellationToken, TResult> action, Context context, CancellationToken cancellationToken)
        {
            if (_syncCacheProvider == null) throw new InvalidOperationException("Please use the synchronous-defined policies when calling the synchronous Execute (and similar) methods.");

            return CacheEngine.Implementation<TResult>(
                _syncCacheProvider.For<TResult>(), 
                _ttlStrategy.For<TResult>(),
                _cacheKeyStrategy, 
                action, 
                context, 
                cancellationToken,
                _onCacheGet, 
                _onCacheMiss, 
                _onCachePut, 
                _onCacheGetError, 
                _onCachePutError);
        }
        public void test(this CachePolicy policy, int a){}
    }

    /// <summary>
    /// A cache policy that can be applied to the results of delegate executions.
    /// </summary>
    public partial class CachePolicy<TResult> : Policy<TResult>, ICachePolicy<TResult>
    {
        public CachePolicy(
            ISyncCacheProvider<TResult> syncCacheProvider, 
            ITtlStrategy<TResult> ttlStrategy,
            Func<Context, string> cacheKeyStrategy,
            Action<Context, string> onCacheGet,
            Action<Context, string> onCacheMiss,
            Action<Context, string> onCachePut,
            Action<Context, string, Exception> onCacheGetError,
            Action<Context, string, Exception> onCachePutError)
            : base((action, context, cancellationToken) => 
                CacheEngine.Implementation(
                    syncCacheProvider, 
                    ttlStrategy, 
                    cacheKeyStrategy,
                    action, 
                    context, 
                    cancellationToken,
                    onCacheGet, 
                    onCacheMiss, 
                    onCachePut, 
                    onCacheGetError, 
                    onCachePutError),
                PredicateHelper.EmptyExceptionPredicates,
                Enumerable.Empty<ResultPredicate<TResult>>())
        { }

    }
}
