using FluentResults;
using System.Text.Json;

namespace HttpClientGenerator.HttpClientBase
{
    public abstract class BaseApiClient
    {
        protected readonly HttpClient _httpClient;

        protected BaseApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        protected async Task<Result<T>> SendRequestAsync<T>(HttpRequestMessage request)
        {
            try
            {
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<T>(content);
                    return Result.Ok(result);
                }

                var problemDetails = await GetProblemDetailsFromResponse(response);

                if (!problemDetails.Any()) return Result.Fail(new Error("No response."));

                var errors = new Error[problemDetails.Count()];
                foreach (var problem in problemDetails)
                {
                    errors.Append(new Error(string.Concat(problem.Code, problem.Message)));
                }

                return Result.Fail<T>(errors);
            }
            catch (Exception ex)
            {
                return Result.Fail<T>(new Error("An error occurred while making the API request."));
            }
        }

        private async Task<ErrorModel[]> GetProblemDetailsFromResponse(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ErrorModel[]>(content);
        }
    }

    public class ErrorModel
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
    }
}