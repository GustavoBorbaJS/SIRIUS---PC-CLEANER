using System.Diagnostics;
using SiriusCleaner.Safety;
using Xunit;

namespace SiriusCleaner.Tests;

public sealed class ProcessGuardTests
{
    private static ProcessKind Classify(string name, int session = 1, bool service = false, string[] extra = null)
    {
        using var self = Process.GetCurrentProcess();
        return new ProcessGuard(extra ?? Array.Empty<string>())
            .Classify(self, name, session, service, @"C:\Apps\x.exe", DateTime.MinValue);
    }

    [Theory]
    [InlineData("dotnet")]
    [InlineData("w3wp")]
    [InlineData("iisexpress")]
    [InlineData("MSBuild")]
    [InlineData("VBCSCompiler")]
    [InlineData("devenv")]
    [InlineData("ServiceHub.Host.dotnet.x64")]
    [InlineData("testhost")]
    public void Recognizes_dotnet_processes(string name) =>
        Assert.Equal(ProcessKind.DotNet, Classify(name));

    [Fact]
    public void Session_zero_and_service_hosts_are_blocked()
    {
        Assert.True(ProcessGuard.IsBlocked(Classify("MinhaApi", session: 0)));
        Assert.True(ProcessGuard.IsBlocked(Classify("MinhaApi", service: true)));
        Assert.True(ProcessGuard.IsBlocked(Classify("explorer")));
        Assert.True(ProcessGuard.IsBlocked(Classify("MinhaApi", extra: new[] { "MinhaApi.exe" })));
    }

    [Fact]
    public void Dev_tools_require_strong_confirmation()
    {
        Assert.True(ProcessGuard.NeedsStrongConfirmation(Classify("dotnet")));
        Assert.True(ProcessGuard.NeedsStrongConfirmation(Classify("node")));
        Assert.True(ProcessGuard.NeedsStrongConfirmation(Classify("sqlservr")));
    }
}
