using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Calcpad.WebApi.Utils.Calcpad;

namespace Calcpad.WebApi.Tests.Utils.Calcpad;

internal static class CalcpadInputHtmlReducerTests
{
    internal static void PreservesFallbackBehavior()
    {
        AssertEqual(string.Empty, CalcpadInputHtmlReducer.Reduce(string.Empty), "Empty HTML");

        const string htmlWithoutInputElements = "<section><p>Calculation result</p></section>";
        AssertEqual(
            htmlWithoutInputElements,
            CalcpadInputHtmlReducer.Reduce(htmlWithoutInputElements),
            "Unmatched HTML"
        );
    }

    internal static void RetainsInputFormElements()
    {
        const string originHtml = """
            <p id="plain">Calculation result</p>
            <header id="header">Not a heading level</header>
            <h1 id="heading-1">Inputs</h1>
            <h6 id="heading-6">Details</h6>
            <h7 id="heading-7">Invalid heading</h7>
            <p id="input-row">Length <input id="length" value="2"><span>m</span></p>
            <select id="material"><option>Steel</option></select>
            <button id="calculate">Calculate</button>
            <img id="diagram" src="diagram.png">
            <p id="error-substring" class="error">Not an exact error class</p>
            <p id="error-row" class="line ERR">Invalid value</p>
            <div class="indent" id="indent">
                <p id="nested-plain">Drop me</p>
                <p id="nested-input-row"><input id="nested-input"></p>
            </div>
            """;

        var reducedDocument = ParseReducedDocument(originHtml);
        AssertPresent(reducedDocument, "#heading-1");
        AssertPresent(reducedDocument, "#heading-6");
        AssertPresent(reducedDocument, "#input-row");
        AssertPresent(reducedDocument, "#length");
        AssertPresent(reducedDocument, "#material");
        AssertPresent(reducedDocument, "#calculate");
        AssertPresent(reducedDocument, "#diagram");
        AssertPresent(reducedDocument, "#error-row");
        AssertPresent(reducedDocument, "#indent");
        AssertPresent(reducedDocument, "#nested-input-row");
        AssertMissing(reducedDocument, "#plain");
        AssertMissing(reducedDocument, "#header");
        AssertMissing(reducedDocument, "#heading-7");
        AssertMissing(reducedDocument, "#error-substring");
        AssertMissing(reducedDocument, "#nested-plain");
    }

    internal static void WrapsNestedConditionalBlocks()
    {
        const string originHtml = """
            <p><span class="cond">#if</span><span class="eq"><var>x</var><sub>1</sub> ≡ 2 <i>m</i></span></p>
            <p><input id="if-input"></p>
            <p><span class="cond">#if</span><span class="eq"><var>z</var> ≥ 1</span></p>
            <p><input id="nested-if-input"></p>
            <p><span class="cond">#end if</span></p>
            <p><span class="cond">#else if</span><span class="eq"><var>y</var> ≠ 3</span></p>
            <p><button id="else-if-button">Continue</button></p>
            <p><span class="cond">#else</span></p>
            <p><select id="else-select"><option>A</option></select></p>
            <p><span class="cond">#end if</span></p>
            """;

        var reducedDocument = ParseReducedDocument(originHtml);
        var conditionalBlocks = reducedDocument.QuerySelectorAll("div.conditional-block");
        AssertEqual(2, conditionalBlocks.Length, "Conditional block count");

        var outerBlock = conditionalBlocks.First(element =>
            element.ParentElement?.LocalName == "body"
        );
        var nestedBlock = conditionalBlocks.First(element => element != outerBlock);
        AssertEqual(
            "x_1==2",
            outerBlock.QuerySelector("div[v-if]")?.GetAttribute("v-if"),
            "If condition"
        );
        AssertEqual(
            "y!=3",
            outerBlock.QuerySelector("div[v-else-if]")?.GetAttribute("v-else-if"),
            "Else-if condition"
        );
        AssertTrue(outerBlock.QuerySelector("div[v-else]") != null, "Else branch should exist.");
        AssertEqual(
            "z>=1",
            nestedBlock
                .Children.FirstOrDefault(element => element.HasAttribute("v-if"))
                ?.GetAttribute("v-if"),
            "Nested if condition"
        );
        AssertPresent(reducedDocument, "#if-input");
        AssertPresent(reducedDocument, "#nested-if-input");
        AssertPresent(reducedDocument, "#else-if-button");
        AssertPresent(reducedDocument, "#else-select");
        AssertMissing(reducedDocument, "span.cond");
    }

    internal static void RecoversAfterMalformedSvg()
    {
        const string originHtml = """
            <svg viewBox="0 0 100 100"><g><path d="M 0 0">
            <p id="input-after-svg">Length <input id="recovered-input"></p>
            <h2 id="heading-after-svg">Recovered heading</h2>
            <p><span class="cond">#if</span><span class="eq"><var>x</var> ≤ 5</span></p>
            <p><button id="conditional-button">Continue</button></p>
            <p><span class="cond">#end if</span></p>
            """;

        var reducedDocument = ParseReducedDocument(originHtml);
        AssertMissing(reducedDocument, "svg");
        AssertPresent(reducedDocument, "#input-after-svg");
        AssertPresent(reducedDocument, "#recovered-input");
        AssertPresent(reducedDocument, "#heading-after-svg");
        AssertPresent(reducedDocument, "#conditional-button");
        AssertEqual(
            "x<=5",
            reducedDocument.QuerySelector("div[v-if]")?.GetAttribute("v-if"),
            "Recovered condition"
        );
    }

    private static IDocument ParseReducedDocument(string originHtml) =>
        new HtmlParser().ParseDocument(CalcpadInputHtmlReducer.Reduce(originHtml));

    private static void AssertPresent(IDocument document, string selector)
    {
        AssertTrue(
            document.QuerySelector(selector) != null,
            "Expected '" + selector + "' to be retained."
        );
    }

    private static void AssertMissing(IDocument document, string selector)
    {
        AssertTrue(
            document.QuerySelector(selector) == null,
            "Expected '" + selector + "' to be removed."
        );
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                name + ": expected '" + expected + "', got '" + actual + "'."
            );
        }
    }
}
