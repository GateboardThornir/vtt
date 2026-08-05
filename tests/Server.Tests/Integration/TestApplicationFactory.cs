using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Vtt.Server.Tests.Integration;

/// <summary>
/// The real application, running on a clock the tests control.
/// </summary>
/// <remarks>
/// Replacing a service this way works, where supplying the connection string this way did not
/// (see <see cref="PostgresFixture"/>): <c>ConfigureWebHost</c> runs when the factory intercepts
/// <c>builder.Build()</c>, which is late for code that already executed but exactly right for
/// dependency injection.
/// <para>
/// Everything else is left alone. A test host that differs from the real one in more than the
/// clock stops being evidence about the real one.
/// </para>
/// </remarks>
internal sealed class TestApplicationFactory(FakeTimeProvider clock) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock));
}
