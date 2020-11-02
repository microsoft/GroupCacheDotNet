using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GroupCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace GroupCacheStubTest
{
    [TestClass]
    public class TestDiskCache
    {
        // Test have random failure disabling for now
        
        [TestMethod]
        public void TestConcurrentCacheAccess()
        {
            var mockFileSystem = new MockFileSystem();
            var diskCache = new DiskCache("c:\\CacheRoot", 10, mockFileSystem);
            long cacheFill = 0;
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 24; i++)
            {
                var t = Task.Run(async () =>
                {
                    for (int k = 0; k < 100; k++)
                    {
                        for (int j = 0; j < 40; j++)
                        {
                            var keyString = j.ToString();
                            var cacheEntry = await diskCache.GetOrAddAsync(keyString,
                           (key, sink, cacheControl) =>
                           {
                               Interlocked.Increment(ref cacheFill);
                               var keystream = key.StringToStream();
                               return keystream.CopyToAsync(sink);
                           }, new CacheControl(), CancellationToken.None);

                            try { 
                                var cacheEntryString = cacheEntry.Value().StreamToString();
                                Assert.AreEqual(keyString, cacheEntryString);
                            } finally
                            {
                                await cacheEntry.DisposeAsync();
                            }
                        }
                    }
                });
                tasks.Add(t);
            }
            Task.WaitAll(tasks.ToArray());
            Assert.AreEqual(10, mockFileSystem.DirectoryGetFiles("c:\\CacheRoot\\tmp").Length); 
        }
        
    }
}
