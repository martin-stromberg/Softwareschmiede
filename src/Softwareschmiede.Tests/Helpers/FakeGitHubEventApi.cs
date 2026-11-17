using System.Text.Json;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Warteschlangen-basierter <see cref="IGitHubEventApiClient"/> für Plugin-Tests:
/// jeder Transportaufruf muss vorab per <see cref="Add"/>/<see cref="AddGraph"/>/<see cref="AddStatus"/>/
/// <see cref="AddFailure"/> eingeplant sein und wird gegen das erwartete Endpunkt-Fragment geprüft;
/// Uploads laufen über eine eigene Warteschlange (<see cref="AddUpload"/> u. a.).</summary>
public class FakeGitHubEventApi : IGitHubEventApiClient
{
    private readonly Queue<(string Endpoint, GitHubEventApiResponse? Response, Exception? Error)> _steps = new();
    private readonly Queue<(string Fragment, GitHubEventApiResponse? Response, Exception? Error)> _uploads = new();
    internal List<GitHubEventApiRequest> Requests { get; } = [];
    internal List<GitHubAssetUploadRequest> UploadRequests { get; } = [];
    internal void Add(string endpoint, object body, Dictionary<string, string>? headers = null) =>
        _steps.Enqueue((endpoint, new(200, JsonSerializer.Serialize(body), headers), null));
    internal void AddJson(string endpoint, string rawBody, Dictionary<string, string>? headers = null) =>
        _steps.Enqueue((endpoint, new(200, rawBody, headers), null));
    internal void AddGraph(object data) => Add("graphql", new { data });
    internal void AddStatus(string endpoint, int status, Dictionary<string, string>? headers = null) =>
        _steps.Enqueue((endpoint, new(status, "{\"message\":\"secret\"}", headers), null));
    internal void AddFailure(string endpoint, Exception error) => _steps.Enqueue((endpoint, null, error));
    internal void AddUpload(string nameFragment, object body, Dictionary<string, string>? headers = null) =>
        _uploads.Enqueue((nameFragment, new(200, JsonSerializer.Serialize(body), headers), null));
    internal void AddUploadJson(string nameFragment, string rawBody) =>
        _uploads.Enqueue((nameFragment, new(200, rawBody), null));
    internal void AddUploadStatus(string nameFragment, int status, Dictionary<string, string>? headers = null) =>
        _uploads.Enqueue((nameFragment, new(status, "{\"message\":\"secret\"}", headers), null));
    internal void AddUploadFailure(string nameFragment, Exception error) => _uploads.Enqueue((nameFragment, null, error));
    internal void AssertComplete()
    {
        Assert.Empty(_steps);
        Assert.Empty(_uploads);
    }
    /// <inheritdoc />
    public Task<GitHubEventApiResponse> SendAsync(GitHubEventApiRequest request, CancellationToken ct = default)
    {
        Requests.Add(request);
        Assert.NotEmpty(_steps);
        var step = _steps.Dequeue();
        Assert.Contains(step.Endpoint, request.Endpoint);
        return step.Error is null ? Task.FromResult(step.Response!) : Task.FromException<GitHubEventApiResponse>(step.Error);
    }
    /// <inheritdoc />
    public Task<GitHubEventApiResponse> UploadAsync(GitHubAssetUploadRequest request, CancellationToken ct = default)
    {
        UploadRequests.Add(request);
        Assert.NotEmpty(_uploads);
        var step = _uploads.Dequeue();
        Assert.Contains(step.Fragment, request.Name);
        return step.Error is null ? Task.FromResult(step.Response!) : Task.FromException<GitHubEventApiResponse>(step.Error);
    }
}
