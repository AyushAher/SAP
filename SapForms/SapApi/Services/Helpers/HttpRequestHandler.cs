using Microsoft.Extensions.Caching.Memory;
using SapApi.Modals;
using System.Text;
using System.Text.Json;

namespace SapApi.Services.Helpers
{
    public class HttpRequestHandler(IMemoryCache cache) : IHttpRequestHandler
    {
        public async Task<T?> GetAsync<T>(string url)
        {
            try
            {

                var sessionId = cache.Get<string>("sessionId");

                var client = new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (string.IsNullOrEmpty(sessionId))
                {
                    throw new ApiErrorException("Unauthorized, session expired.");
                }

                request.Headers.Add("Cookie", $"B1SESSION={sessionId}; ROUTEID=.node2");

                HttpResponseMessage response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApiErrorException(await response.Content.ReadAsStringAsync());
                }

                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return default;
            }

        }

        public async Task<TResponse?> PatchAsync<TRequest, TResponse>(string url, TRequest data)
        {
            try
            {
                var sessionId = cache.Get<string>("sessionId");

                var client = new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });

                var request = new HttpRequestMessage(HttpMethod.Patch, url);

                if (string.IsNullOrEmpty(sessionId))
                {
                    request.Headers.Add("Cookie", "ROUTEID=.node2");
                }
                else
                {
                    request.Headers.Add("Cookie", $"B1SESSION={sessionId}; ROUTEID=.node2");
                }
                request.Content = new StringContent(JsonSerializer.Serialize(data));

                HttpResponseMessage response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    try
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (string.IsNullOrEmpty(json))
                        {
                            return default;
                        }

                        return JsonSerializer.Deserialize<TResponse>(json);
                    }
                    catch
                    {

                        throw new ApiErrorException(await response.Content.ReadAsStringAsync());
                    }

                }

                return await response.Content.ReadFromJsonAsync<TResponse>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return default;
            }

        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest? data)
        {
            try
            {
                var client = new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                if (url.Contains(Constants.SapServiceLayerUrl))
                {
                    var sessionId = cache.Get<string>("sessionId");
                    request.Headers.Add("Cookie",
                        string.IsNullOrEmpty(sessionId) ?
                            "ROUTEID=.node2" : $"B1SESSION={sessionId}; ROUTEID=.node2");
                }

                if (data is not null)
                    request.Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<TResponse>();
                try
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<TResponse>(json);
                }
                catch
                {
                    throw new ApiErrorException(await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return default;
            }
        }

        public async Task<TResponse?> PutAsync<TRequest, TResponse>(string url, TRequest data)
        {
            try
            {
                var client = new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });

                var request = new HttpRequestMessage(HttpMethod.Put, url);
                var sessionId = cache.Get<string>("sessionId");
                request.Headers.Add("Cookie",
                    string.IsNullOrEmpty(sessionId)
                        ? "ROUTEID=.node2"
                        : $"B1SESSION={sessionId}; ROUTEID=.node2");

                var json = JsonSerializer.Serialize(data);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<TResponse>();

                try
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<TResponse>(errorJson);
                }
                catch
                {
                    throw new ApiErrorException(await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return default;
            }
        }
    }
}