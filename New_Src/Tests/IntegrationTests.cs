using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Tests;

public class IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public IntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact(DisplayName = "Save -> Load project works")]
    public async Task Save_Then_Load_Project()
    {
        var projectId = Guid.NewGuid();
        var saveBody = new
        {
            ProjectName = "IT_SaveLoad",
            Owner = "test",
            RawRows = new[] { new { ColumnData = "{\"Fe\":1.2}", SampleId = "S1" } },
            StateJson = "{}"
        };

        var saveRes = await _client.PostAsJsonAsync($"/api/projects/{projectId}/save", saveBody);
        saveRes.EnsureSuccessStatusCode();

        var loadRes = await _client.GetAsync($"/api/projects/{projectId}/load");
        loadRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var loadContent = await loadRes.Content.ReadFromJsonAsync<dynamic>();
        ((string)loadContent.data.projectName).Should().Be("IT_SaveLoad");
    }

    [Fact(DisplayName = "Import CSV (sync) creates project and rows")]
    public async Task ImportCsv_Sync_CreatesProject()
    {
        // prepare csv content
        var csv = "SampleId,Fe,Cu\nS1,12.3,0.5\nS2,11.9,0.6\n";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        content.Add(fileContent, "file", "data.csv");
        content.Add(new StringContent("ImportedFromTest"), "projectName");
        content.Add(new StringContent("dev"), "owner");

        var res = await _client.PostAsync("/api/projects/import", content);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<dynamic>();
        ((bool)body.succeeded).Should().BeTrue();
        Guid projectId = Guid.Parse((string)body.data.projectId);

        // load and verify rows
        var loadRes = await _client.GetAsync($"/api/projects/{projectId}/load");
        loadRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var loadBody = await loadRes.Content.ReadFromJsonAsync<dynamic>();
        ((string)loadBody.data.projectName).Should().Be("ImportedFromTest");
        // RawRows should be present and have count 2
        var rawRows = loadBody.data.rawRows;
        ((int)rawRows.Count).Should().Be(2);
    }

    [Fact(DisplayName = "Process project computes and saves a ProjectState")]
    public async Task ProcessProject_ProducesState()
    {
        // First create a project via Save endpoint with several rows
        var projectId = Guid.NewGuid();
        var saveBody = new
        {
            ProjectName = "IT_Process",
            Owner = "proc",
            RawRows = new[]
            {
                new { ColumnData = "{\"Fe\":10.0,\"Cu\":0.5}", SampleId = "s1" },
                new { ColumnData = "{\"Fe\":12.0,\"Cu\":0.7}", SampleId = "s2" },
                new { ColumnData = "{\"Fe\":11.0,\"Cu\":0.6}", SampleId = "s3" }
            },
            StateJson = "{}"
        };

        var saveRes = await _client.PostAsJsonAsync($"/api/projects/{projectId}/save", saveBody);
        saveRes.EnsureSuccessStatusCode();

        // Call processing (sync)
        var procRes = await _client.PostAsync($"/api/projects/{projectId}/process", null);
        procRes.EnsureSuccessStatusCode();

        var procBody = await procRes.Content.ReadFromJsonAsync<dynamic>();
        ((bool)procBody.succeeded).Should().BeTrue();
        // ProjectStateId returned
        var stateId = (int)procBody.data.projectStateId;

        // Optionally: fetch latest state via load endpoint and check LatestStateJson exists
        var loadRes = await _client.GetAsync($"/api/projects/{projectId}/load");
        loadRes.EnsureSuccessStatusCode();
        var loadBody = await loadRes.Content.ReadFromJsonAsync<dynamic>();
        ((string)loadBody.data.projectName).Should().Be("IT_Process");
        ((string)loadBody.data.latestStateJson).Should().NotBeNullOrEmpty();
    }
}