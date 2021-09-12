
namespace Test{
    class Test1 {

    public void test(){ 
            const string valueToReturn = "valueToReturn";
            const string operationKey = "SomeOperationKey";

            IAsyncCacheProvider stubCacheProvider = new StubCacheProvider();
            var cache = Policy.CacheAsync<string>(stubCacheProvider, TimeSpan.MaxValue);

            (bool cacheHit1, object fromCache1) = await stubCacheProvider.TryGetAsync(operationKey, CancellationToken.None, false).ConfigureAwait(false);
            cacheHit1.Should().BeFalse();
            fromCache1.Should().BeNull();

            (await cache.ExecuteAsync(async ctx => { await TaskHelper.EmptyTask.ConfigureAwait(false); return valueToReturn; }, new Context(operationKey)).ConfigureAwait(false)).Should().Be(valueToReturn);

            (bool cacheHit2, object fromCache2) = await stubCacheProvider.TryGetAsync(operationKey, CancellationToken.None, false).ConfigureAwait(false);
            cacheHit2.Should().BeTrue();
            fromCache2.Should().Be(valueToReturn);
     }
  }
}
