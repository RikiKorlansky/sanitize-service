using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SanitizeService.Application;
using SanitizeService.Application.Exceptions;

namespace SanitizeService.Tests;

public sealed class SanitizationServiceTests
{
    [Fact]
    public async Task Unknown_format_throws_unsupported()
    {
        using var provider = BuildProvider();
        var sut = provider.GetRequiredService<ISanitizationService>();
        var input = new MemoryStream("999"u8.ToArray());
        await Assert.ThrowsAsync<UnsupportedFormatException>(async () =>
            await sut.SanitizeAsync(input, CancellationToken.None));
    }

    [Fact]
    public async Task Abc_file_is_sanitized_end_to_end()
    {
        using var provider = BuildProvider();
        var sut = provider.GetRequiredService<ISanitizationService>();
        // Strict 3-byte blocks: A1C valid, AFC invalid → A255C
        var input = new MemoryStream("123A1CAFC789"u8.ToArray());
        await using var output = await sut.SanitizeAsync(input, CancellationToken.None);
        using var reader = new StreamReader(output, leaveOpen: true);
        var text = await reader.ReadToEndAsync();
        Assert.Equal("123A1CA255C789", text);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.ClearProviders());
        services.Configure<SanitizationOptions>(o => o.MaxFileSizeBytes = 1024 * 1024);
        services.AddSanitization();
        return services.BuildServiceProvider();
    }
}
