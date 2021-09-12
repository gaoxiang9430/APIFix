namespace Test{
    class Test1 {

    public void test(){
            const string valueToReturn = "valueToReturn";
            const string operationKey = "SomeOperationKey";

            IAsyncCacheProvider stubCacheProvider = new StubCacheProvider();
            CachePolicy<string> cache = Policy.CacheAsync<string>(stubCacheProvider, TimeSpan.MaxValue);

            ((string)await stubCacheProvider.GetAsync(operationKey, CancellationToken.None, false).ConfigureAwait(false)).Should().BeNull();

            (await cache.ExecuteAsync(async ctx => { await TaskHelper.EmptyTask.ConfigureAwait(false); return valueToReturn; }, new Context(operationKey)).ConfigureAwait(false)).Should().Be(valueToReturn);

            ((string)await stubCacheProvider.GetAsync(operationKey, CancellationToken.None, false).ConfigureAwait(false)).Should().Be(valueToReturn);
        }
  }
}
