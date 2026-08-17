namespace Calcpad.Tests;

public class ExpressionParserProgressTests
{
    [Fact]
    public void Progress_RaisesEventWithoutOutput()
    {
        var parser = new ExpressionParser();
        ProgressEventArgs args = null;
        parser.Progress += (_, e) => args = e;

        parser.Parse("""
            a = 1
            progress(0.25; Loading)
            b = 2
            """);

        Assert.NotNull(args);
        Assert.Equal(0.25, args.Value, 12);
        Assert.Equal("Loading", args.Message);
        Assert.DoesNotContain("progress", parser.HtmlResult, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Loading", parser.HtmlResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Progress_AllowsEmptyMessage()
    {
        var parser = new ExpressionParser();
        ProgressEventArgs args = null;
        parser.Progress += (_, e) => args = e;

        parser.Parse("progress(1)");

        Assert.NotNull(args);
        Assert.Equal(1d, args.Value, 12);
        Assert.Equal(string.Empty, args.Message);
        Assert.True(string.IsNullOrWhiteSpace(parser.HtmlResult));
    }

    [Fact]
    public void Progress_StripsQuotedMessage()
    {
        var parser = new ExpressionParser();
        ProgressEventArgs args = null;
        parser.Progress += (_, e) => args = e;

        parser.Parse("progress(0.5; \"Loading data\")");

        Assert.NotNull(args);
        Assert.Equal(0.5, args.Value, 12);
        Assert.Equal("Loading data", args.Message);
        Assert.True(string.IsNullOrWhiteSpace(parser.HtmlResult));
    }

    [Fact]
    public void Progress_RaisesEventForEachLoopIteration()
    {
        var parser = new ExpressionParser();
        var values = new List<double>();
        parser.Progress += (_, e) => values.Add(e.Value);

        parser.Parse("""
            #for i = 1 : 3
            progress(i / 3; step)
            #loop
            """);

        Assert.Equal([1d / 3d, 2d / 3d, 1d], values);
    }

    [Fact]
    public void Progress_RejectsOutOfRangeValue()
    {
        var parser = new ExpressionParser();
        var eventCount = 0;
        parser.Progress += (_, _) => ++eventCount;

        parser.Parse("progress(1.5; invalid)");

        Assert.Equal(0, eventCount);
        Assert.Contains("err", parser.HtmlResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Progress_RejectsVectorValue()
    {
        var parser = new ExpressionParser();
        var eventCount = 0;
        parser.Progress += (_, _) => ++eventCount;

        parser.Parse("progress([0.5; 0.6])");

        Assert.Equal(0, eventCount);
        Assert.Contains("err", parser.HtmlResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Progress_IsOnlyStandalone()
    {
        var parser = new ExpressionParser();
        var eventCount = 0;
        parser.Progress += (_, _) => ++eventCount;

        parser.Parse("x = progress(0.5)");

        Assert.Equal(0, eventCount);
        Assert.Contains("err", parser.HtmlResult, StringComparison.OrdinalIgnoreCase);
    }
}
