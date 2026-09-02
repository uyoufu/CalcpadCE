using Calcpad.WebApi.Services.Calcpad;
using HtmlAgilityPack;

namespace Calcpad.WebApi.Tests.Services.Calcpad;

internal static class CpdContentServiceTests
{
    internal static void PreservesConditionalChainWhenOnlyElseBranchHasInputs()
    {
        const string originHtml = """
            <p><span class="cond">#if</span><span class="eq"><var>opt</var> ≥ 4</span></p>
            <div class="indent"><p>剪应力检算结果</p></div>
            <p><span class="cond">#else</span></p>
            <div class="indent">
              <strong>箍筋材料特性</strong>
              <p><select name="箍筋材料特性"><option>HPB300</option></select></p>
              <p>直径 <input id="stirrup-diameter" value="8"></p>
              <p><span class="cond">#if</span><span class="eq"><var>Q</var> ≤ <var>V</var></span></p>
              <div class="indent"><p>抗剪验算通过</p></div>
              <p><span class="cond">#else</span></p>
              <div class="indent"><p>抗剪验算不通过</p></div>
              <p><span class="cond">#end if</span></p>
            </div>
            <p><span class="cond">#end if</span></p>
            """;

        var contentService = new CpdContentService(null!, null!, null!);
        var simplifiedHtml = contentService.SimplifyHtml(originHtml);

        var document = new HtmlDocument();
        document.LoadHtml(simplifiedHtml);

        var conditionalBlock = document.DocumentNode.SelectSingleNode(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' conditional-block ')]"
        );
        AssertNotNull(conditionalBlock, "Conditional block should be retained.");
        AssertNotNull(
            conditionalBlock!.SelectSingleNode("./div[@v-if='opt>=4']"),
            "The empty if branch should remain before the else branch."
        );
        var elseBranch = conditionalBlock.SelectSingleNode("./div[@v-else]");
        AssertNotNull(elseBranch, "Else branch should be retained.");
        AssertNotNull(
            elseBranch!.SelectSingleNode(".//select[@name='箍筋材料特性']"),
            "Stirrup material select should be retained."
        );
        AssertNotNull(
            elseBranch.SelectSingleNode(".//input[@id='stirrup-diameter']"),
            "Stirrup diameter input should be retained."
        );
        AssertTrue(
            document.DocumentNode.SelectSingleNode("//span[@class='cond']") == null,
            "Conditional markers should be removed."
        );
    }

    internal static void PreservesConditionalSkeletonWhenAllBranchesAreEmpty()
    {
        const string originHtml = """
            <p><span class="cond">#if</span><span class="eq"><var>mode</var> ≡ 1</span></p>
            <div class="indent"><p>First calculation result</p></div>
            <p><span class="cond">#else if</span><span class="eq"><var>mode</var> ≡ 2</span></p>
            <div class="indent"><p>Second calculation result</p></div>
            <p><span class="cond">#else</span></p>
            <div class="indent"><p>Fallback calculation result</p></div>
            <p><span class="cond">#end if</span></p>
            """;

        var contentService = new CpdContentService(null!, null!, null!);
        var simplifiedHtml = contentService.SimplifyHtml(originHtml);

        var document = new HtmlDocument();
        document.LoadHtml(simplifiedHtml);

        var conditionalBlock = document.DocumentNode.SelectSingleNode(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' conditional-block ')]"
        );
        AssertNotNull(conditionalBlock, "Empty conditional skeleton should be retained.");

        var ifBranch = conditionalBlock!.SelectSingleNode("./div[@v-if='mode==1']");
        var elseIfBranch = conditionalBlock.SelectSingleNode("./div[@v-else-if='mode==2']");
        var elseBranch = conditionalBlock.SelectSingleNode("./div[@v-else]");
        AssertNotNull(ifBranch, "Empty if branch should be retained.");
        AssertNotNull(elseIfBranch, "Empty else-if branch should be retained.");
        AssertNotNull(elseBranch, "Empty else branch should be retained.");
        AssertTrue(ifBranch!.ChildNodes.Count == 0, "If branch content should be removed.");
        AssertTrue(elseIfBranch!.ChildNodes.Count == 0, "Else-if branch content should be removed.");
        AssertTrue(elseBranch!.ChildNodes.Count == 0, "Else branch content should be removed.");
        AssertTrue(
            document.DocumentNode.SelectSingleNode("//span[@class='cond']") == null,
            "Conditional markers should be removed."
        );
    }

    private static void AssertNotNull(object? value, string message)
    {
        if (value == null)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
