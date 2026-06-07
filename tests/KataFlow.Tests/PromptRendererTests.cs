using KataFlow.Core.Abstractions;
using KataFlow.Engine;
using NSubstitute;

namespace KataFlow.Tests;

[Trait("Category", "Unit")]
public class PromptRendererTests
{
    [Fact]
    public void Render_ReplacesVariables()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("template.md").Returns(true);
        fs.ReadAllTextAsync("template.md", default).Returns("Hello {{name}}!");

        var renderer = new PromptRenderer(fs);
        var result = renderer.Render("template.md", new Dictionary<string, string> { ["name"] = "World" });

        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void Render_MissingVariable_Throws()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("template.md").Returns(true);
        fs.ReadAllTextAsync("template.md", default).Returns("Hello {{missing}}!");

        var renderer = new PromptRenderer(fs);

        Assert.Throws<InvalidOperationException>(() =>
            renderer.Render("template.md", new Dictionary<string, string>()));
    }

    [Fact]
    public void Render_MissingTemplate_Throws()
    {
        var fs = Substitute.For<IFileSystem>();
        fs.FileExists("nonexistent.md").Returns(false);

        var renderer = new PromptRenderer(fs);

        Assert.Throws<FileNotFoundException>(() =>
            renderer.Render("nonexistent.md", new Dictionary<string, string>()));
    }
}
