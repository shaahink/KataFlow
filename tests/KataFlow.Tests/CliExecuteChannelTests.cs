using KataFlow.Adapters.CliExecute;
using KataFlow.Core.Abstractions;
using KataFlow.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace KataFlow.Tests;

[Trait("Category", "Unit")]
public class CliExecuteChannelTests
{
    private static IFileSystem CreateFileSystem(string? templateContent = null)
    {
        var fs = Substitute.For<IFileSystem>();
        fs.GetCurrentDirectory().Returns(Environment.CurrentDirectory);
        fs.Combine(Arg.Any<string>(), Arg.Any<string>())
            .Returns(c => Path.Combine(c.ArgAt<string>(0), c.ArgAt<string>(1)));
        fs.Combine(Arg.Any<string[]>())
            .Returns(c => Path.Combine(c.Arg<string[]>()));

        fs.CreateDirectory(Arg.Any<string>());
        var instructionsPath = Path.Combine(Environment.CurrentDirectory, "templates", "_system", "output-instructions.md");
        fs.FileExists(instructionsPath).Returns(templateContent is not null);
        if (templateContent is not null)
            fs.ReadAllTextAsync(instructionsPath).Returns(templateContent);
        fs.WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return fs;
    }

    [Fact]
    public void Constructor_SetsChannelType_ToCliExecute()
    {
        var logger = Substitute.For<ILogger<CliExecuteChannel>>();
        var fs = Substitute.For<IFileSystem>();
        var opts = Options.Create(new CliExecuteOptions());

        var channel = new CliExecuteChannel(logger, fs, opts);

        Assert.Equal(Core.Enums.ChannelType.CliExecute, channel.Type);
    }

    [Fact]
    public async Task SendAsync_Stdin_WritesPromptToStdin()
    {
        var logger = Substitute.For<ILogger<CliExecuteChannel>>();
        var fs = CreateFileSystem();
        var opts = Options.Create(new CliExecuteOptions
        {
            Command = "cmd",
            Arguments = "/c echo TEST_STDIN_OK",
            InputMode = CliInputMode.Stdin,
            TimeoutSeconds = 10,
        });

        var channel = new CliExecuteChannel(logger, fs, opts);
        var request = new AgentRequest
        {
            SessionId = "test-sess",
            StepName = "test-step",
            RenderedPrompt = "Hello, World",
        };

        var result = await channel.SendAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Expected success but got: {result.ErrorMessage}");
        Assert.Contains("TEST_STDIN_OK", result.Content);
        Assert.Equal("0", result.Metadata["exit_code"]);
    }

    [Fact]
    public async Task SendAsync_File_WritesFileAndPassesPath()
    {
        var logger = Substitute.For<ILogger<CliExecuteChannel>>();
        var fs = CreateFileSystem();
        var opts = Options.Create(new CliExecuteOptions
        {
            Command = "cmd",
            Arguments = "/c echo TEST_FILE_OK",
            InputMode = CliInputMode.File,
            TimeoutSeconds = 10,
        });

        var channel = new CliExecuteChannel(logger, fs, opts);
        var request = new AgentRequest
        {
            SessionId = "test-sess",
            StepName = "test-step",
            RenderedPrompt = "Hello from file mode",
        };

        var result = await channel.SendAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Expected success but got: {result.ErrorMessage}");
        Assert.Contains("TEST_FILE_OK", result.Content);
        await fs.Received(1).WriteAllTextAsync(
            Arg.Is<string>(p => p.Contains("input-test-step.md")),
            Arg.Is<string>(s => s.Contains("Hello from file mode")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_AppendsOutputInstructions_WhenTemplateExists()
    {
        var logger = Substitute.For<ILogger<CliExecuteChannel>>();
        var fs = CreateFileSystem("## Output\nWrite to {{{{{_output_path}}}}} for {{{{{_session_id}}}}}");
        var opts = Options.Create(new CliExecuteOptions
        {
            Command = "cmd",
            Arguments = "/c echo TEST_INSTRUCTIONS_OK",
            InputMode = CliInputMode.Stdin,
            TimeoutSeconds = 10,
        });

        var channel = new CliExecuteChannel(logger, fs, opts);
        var request = new AgentRequest
        {
            SessionId = "s1",
            StepName = "plan",
            RenderedPrompt = "Do the thing",
        };

        var result = await channel.SendAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Expected success but got: {result.ErrorMessage}");
        Assert.Contains("TEST_INSTRUCTIONS_OK", result.Content);
    }

    [Fact]
    public async Task SendAsync_MissingCommand_ReturnsFailure()
    {
        var logger = Substitute.For<ILogger<CliExecuteChannel>>();
        var fs = CreateFileSystem();
        var opts = Options.Create(new CliExecuteOptions
        {
            Command = "nonexistentcommand123xyz",
            Arguments = "",
            InputMode = CliInputMode.Stdin,
            TimeoutSeconds = 5,
        });

        var channel = new CliExecuteChannel(logger, fs, opts);
        var request = new AgentRequest
        {
            SessionId = "s1",
            StepName = "bad",
            RenderedPrompt = "anything",
        };

        var result = await channel.SendAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage!);
    }

    [Fact]
    public async Task SendAsync_NonZeroExitCode_ReturnsFailure()
    {
        var logger = Substitute.For<ILogger<CliExecuteChannel>>();
        var fs = CreateFileSystem();
        var opts = Options.Create(new CliExecuteOptions
        {
            Command = "cmd",
            Arguments = "/c exit 1",
            InputMode = CliInputMode.Stdin,
            TimeoutSeconds = 5,
        });

        var channel = new CliExecuteChannel(logger, fs, opts);
        var request = new AgentRequest
        {
            SessionId = "s1",
            StepName = "fail-step",
            RenderedPrompt = "fail",
        };

        var result = await channel.SendAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("1", result.Metadata["exit_code"]);
    }

    [Fact]
    public async Task SendAsync_SkipsOutputInstructions_WhenTemplateMissing()
    {
        var logger = Substitute.For<ILogger<CliExecuteChannel>>();
        var fs = CreateFileSystem(); // no template
        var opts = Options.Create(new CliExecuteOptions
        {
            Command = "cmd",
            Arguments = "/c echo TEST_NO_TEMPLATE_OK",
            InputMode = CliInputMode.Stdin,
            TimeoutSeconds = 10,
        });

        var channel = new CliExecuteChannel(logger, fs, opts);
        var request = new AgentRequest
        {
            SessionId = "s1",
            StepName = "plan",
            RenderedPrompt = "Do the thing",
        };

        var result = await channel.SendAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Expected success but got: {result.ErrorMessage}");
        Assert.Contains("TEST_NO_TEMPLATE_OK", result.Content);
    }
}
