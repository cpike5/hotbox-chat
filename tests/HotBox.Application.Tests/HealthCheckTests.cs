using System.Net;
using HotBox.Application.Tests.Fixtures;

namespace HotBox.Application.Tests;

public class HealthCheckTests : IClassFixture<HotBoxWebApplicationFactory>
{
    private readonly HotBoxWebApplicationFactory _factory;

    public HealthCheckTests(HotBoxWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
