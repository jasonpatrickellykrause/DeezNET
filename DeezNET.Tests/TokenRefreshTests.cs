using System.Net;
using System.Web;
using Shouldly;

namespace DeezNET.Tests
{
    public class TokenRefreshTests
    {
        [Fact]
        public async Task ConcurrentStaleRefreshesShareOneRequest()
        {
            FakeDeezer deezer = new();
            GWApi gw = new(new HttpClient(deezer), "arl");

            long stale = gw.TokenVersion;
            await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => gw.SetToken(staleVersion: stale)));

            deezer.UserDataCalls.ShouldBe(1);
            gw._apiToken.ShouldBe("fresh");
        }

        [Fact]
        public async Task ForcedRefreshesAlwaysRequest()
        {
            FakeDeezer deezer = new();
            GWApi gw = new(new HttpClient(deezer), "arl");

            await gw.SetToken(force: true);
            await gw.SetToken(force: true);

            deezer.UserDataCalls.ShouldBe(2);
        }

        [Fact]
        public async Task RefreshAfterAnotherCallerRefreshedIsSkipped()
        {
            FakeDeezer deezer = new();
            GWApi gw = new(new HttpClient(deezer), "arl");

            long stale = gw.TokenVersion;
            await gw.SetToken(staleVersion: stale);
            // this caller's request used the old token too, but the refresh it needs already happened
            await gw.SetToken(staleVersion: stale);

            deezer.UserDataCalls.ShouldBe(1);
        }

        [Fact]
        public async Task ConcurrentCallsRejectedForAnExpiredTokenRefreshOnce()
        {
            FakeDeezer deezer = new();
            GWApi gw = new(new HttpClient(deezer), "arl") { _apiToken = "expired" };

            await Task.WhenAll(Enumerable.Range(0, 3).Select(i => gw.GetTrack(i)));

            deezer.UserDataCalls.ShouldBe(1);
        }

        // answers deezer.getUserData with a fresh token, and rejects other methods unless they use it
        private sealed class FakeDeezer : HttpMessageHandler
        {
            private int _userDataCalls;
            public int UserDataCalls => _userDataCalls;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);

                string body;
                if (query["method"] == "deezer.getUserData")
                {
                    Interlocked.Increment(ref _userDataCalls);
                    // long enough for the other callers to queue up behind the lock
                    await Task.Delay(100, cancellationToken);
                    body = """{"error":[],"results":{"checkForm":"fresh","USER":{"USER_ID":1}}}""";
                }
                else if (query["api_token"] != "fresh")
                {
                    body = """{"error":{"VALID_TOKEN_REQUIRED":"Invalid CSRF token"},"results":{}}""";
                }
                else
                {
                    body = """{"error":[],"results":{"SNG_ID":"1"}}""";
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
            }
        }
    }
}
