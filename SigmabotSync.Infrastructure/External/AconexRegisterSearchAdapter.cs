using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SigmabotSync.Domain.Models.Extraction;
using SigmabotSync.Domain.Ports;

namespace SigmabotSync.Infrastructure.External
{
    /// <summary>POST .../register/search con Basic únicamente (sin X-Application-Key).</summary>
    public sealed class AconexRegisterSearchAdapter : IAconexRegisterSearchPort, IDisposable
    {
        private readonly HttpClient _httpClient;

        public AconexRegisterSearchAdapter()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
        }

        public async Task<Rootobject> SearchRegisterPageAsync(
            string baseUrl,
            string projectId,
            string orgId,
            string userId,
            string authorizationHeaderBase64,
            IReadOnlyList<string> returnFields,
            int resultSize,
            int pageNumber,
            bool throwIfNotSuccess = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("baseUrl requerido.", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("projectId requerido.", nameof(projectId));

            string root = baseUrl.TrimEnd('/');
            string uri = $"{root}/api/projects/{projectId}/register/search";

            var body = new
            {
                orgId = orgId,
                userId = userId,
                returnFields = returnFields?.ToList() ?? new List<string>(),
                resultSize = resultSize.ToString(),
                showDocHistory = "true",
                pageNumber = pageNumber.ToString()
            };

            string jsonBody = JsonConvert.SerializeObject(body);
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authorizationHeaderBase64);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        if (!throwIfNotSuccess)
                            return null;
                        response.EnsureSuccessStatusCode();
                    }

                    string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    responseString = responseString.Replace("\u0003", "");
                    return JsonConvert.DeserializeObject<Rootobject>(responseString);
                }
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
