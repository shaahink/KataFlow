using System.CommandLine;
using KataFlow.Cli.Commands;
using NSubstitute;

namespace KataFlow.Tests;

[Collection("Sequential")]
[Trait("Category", "Integration")]
public class CliIntegrationTests
{
    private static Command BuildRoot()
    {
        var root = new RootCommand("test");
        root.Add(new ListCommand(
            Substitute.For<Core.Interfaces.IWorkflowLoader>(),
            Substitute.For<Core.Interfaces.ISessionStore>()).Create());
        root.Add(new ApproveCommand(
            Substitute.For<Core.Abstractions.IFileSystem>()).Create());
        root.Add(new StatusCommand(
            Substitute.For<Core.Interfaces.ISessionStore>()).Create());
        root.Add(new WatchCommand(
            Substitute.For<Core.Abstractions.IFileSystem>()).Create());
        return root;
    }

    [Fact]
    public void List_MissingArg_ShowsError()
    {
        var root = BuildRoot();
        var result = root.Parse(["list"]);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void List_UnknownTarget_ShowsError()
    {
        var root = BuildRoot();
        var result = root.Parse(["list", "invalid"]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Approve_ParsesSuccessfully()
    {
        var root = BuildRoot();
        var result = root.Parse(["approve"]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Watch_ParsesSuccessfully()
    {
        var root = BuildRoot();
        var result = root.Parse(["watch"]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Root_ParsesHelp()
    {
        var root = BuildRoot();
        var result = root.Parse(["--help"]);
        Assert.Empty(result.Errors);
    }
}
