using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Calcpad.WebApi.Utils.Calcpad
{
    /// <summary>
    /// Reduces Calcpad calculation HTML to the headings, errors, controls, and conditional
    /// containers required by the input form. AngleSharp is used so malformed foreign content,
    /// such as SVG, is recovered according to HTML5 parsing rules before reduction.
    /// </summary>
    public static class CalcpadInputHtmlReducer
    {
        private const string HtmlNamespaceUri = "http://www.w3.org/1999/xhtml";
        private const string ConditionalBlockClass = "conditional-block";
        private const string IndentClass = "indent";
        private const string ErrorClass = "err";
        private const string ConditionalMarkerClass = "cond";
        private const string EquationClass = "eq";
        private const string VueIfAttribute = "v-if";
        private const string VueElseIfAttribute = "v-else-if";
        private const string VueElseAttribute = "v-else";

        private static readonly HashSet<string> SupportedElementNames =
            new(StringComparer.OrdinalIgnoreCase) { "input", "select", "button", "img", };

        private static readonly HashSet<string> HeadingElementNames =
            new(StringComparer.OrdinalIgnoreCase) { "h1", "h2", "h3", "h4", "h5", "h6", };

        /// <summary>
        /// Reduces a Calcpad HTML fragment while preserving the original input when no relevant
        /// elements can be found, preventing a malformed or unexpected result from becoming blank.
        /// </summary>
        /// <param name="originHtml">The Calcpad calculation HTML fragment.</param>
        /// <returns>The reduced HTML fragment, or the original fragment when nothing is retained.</returns>
        public static string Reduce(string originHtml)
        {
            if (string.IsNullOrEmpty(originHtml))
            {
                return string.Empty;
            }

            var document = new HtmlParser().ParseDocument(originHtml);
            var body = document.Body;
            if (body == null)
            {
                return originHtml;
            }

            WrapConditionalBlocks(document, body);

            var reducedHtml = new StringBuilder(originHtml.Length);
            foreach (var sourceNode in body.ChildNodes.ToArray())
            {
                var clonedNode = sourceNode.Clone(true);
                if (RetainNodeRecursively(clonedNode) && clonedNode is IElement retainedElement)
                {
                    reducedHtml.Append(retainedElement.OuterHtml);
                }
            }

            return reducedHtml.Length == 0 ? originHtml : reducedHtml.ToString();
        }

        private static void WrapConditionalBlocks(IDocument document, INode container)
        {
            if (!container.HasChildNodes)
            {
                return;
            }

            var siblings = container.ChildNodes.ToArray();
            var siblingIndex = 0;
            while (siblingIndex < siblings.Length)
            {
                var startNode = siblings[siblingIndex];
                if (
                    TryGetConditionalMarker(startNode, out var markerType, out _)
                    && markerType == ConditionalMarkerType.If
                )
                {
                    var block = TryCreateConditionalBlock(document, siblings, siblingIndex);
                    if (block.HasValue)
                    {
                        container.ReplaceChild(block.Value.Wrapper, startNode);
                        foreach (var markerNode in block.Value.MarkerNodes)
                        {
                            markerNode.Parent?.RemoveChild(markerNode);
                        }

                        siblings = container.ChildNodes.ToArray();
                        continue;
                    }
                }

                siblingIndex++;
            }

            foreach (var childElement in container.ChildNodes.OfType<IElement>().ToArray())
            {
                WrapConditionalBlocks(document, childElement);
            }
        }

        private static (IElement Wrapper, List<INode> MarkerNodes)? TryCreateConditionalBlock(
            IDocument document,
            IReadOnlyList<INode> siblings,
            int startIndex
        )
        {
            var startNode = siblings[startIndex];
            if (
                !TryGetConditionalMarker(startNode, out var startType, out var startCondition)
                || startType != ConditionalMarkerType.If
            )
            {
                return null;
            }

            var branches =
                new List<(
                    ConditionalMarkerType Type,
                    string Condition,
                    List<INode> ContentNodes
                )>();
            var markerNodes = new List<INode> { startNode };
            var currentType = ConditionalMarkerType.If;
            var currentCondition = startCondition;
            var currentContentNodes = new List<INode>();
            var nestedBlockDepth = 0;
            var hasEndMarker = false;

            for (var index = startIndex + 1; index < siblings.Count; index++)
            {
                var candidateNode = siblings[index];
                if (!TryGetConditionalMarker(candidateNode, out var markerType, out var condition))
                {
                    currentContentNodes.Add(candidateNode);
                    continue;
                }

                if (markerType == ConditionalMarkerType.If)
                {
                    nestedBlockDepth++;
                    currentContentNodes.Add(candidateNode);
                    continue;
                }

                if (markerType == ConditionalMarkerType.EndIf)
                {
                    if (nestedBlockDepth > 0)
                    {
                        nestedBlockDepth--;
                        currentContentNodes.Add(candidateNode);
                        continue;
                    }

                    branches.Add((currentType, currentCondition, currentContentNodes));
                    markerNodes.Add(candidateNode);
                    hasEndMarker = true;
                    break;
                }

                if (
                    nestedBlockDepth == 0
                    && markerType is ConditionalMarkerType.ElseIf or ConditionalMarkerType.Else
                )
                {
                    branches.Add((currentType, currentCondition, currentContentNodes));
                    currentType = markerType;
                    currentCondition =
                        markerType == ConditionalMarkerType.Else ? string.Empty : condition;
                    currentContentNodes = new List<INode>();
                    markerNodes.Add(candidateNode);
                    continue;
                }

                currentContentNodes.Add(candidateNode);
            }

            if (!hasEndMarker)
            {
                return null;
            }

            var wrapper = document.CreateElement("div");
            wrapper.ClassList.Add(ConditionalBlockClass);
            foreach (var branch in branches)
            {
                var branchElement = document.CreateElement("div");
                SetConditionalBranchAttribute(branchElement, branch.Type, branch.Condition);
                foreach (var contentNode in branch.ContentNodes)
                {
                    contentNode.Parent?.RemoveChild(contentNode);
                    branchElement.AppendChild(contentNode);
                }

                wrapper.AppendChild(branchElement);
            }

            return (wrapper, markerNodes);
        }

        private static void SetConditionalBranchAttribute(
            IElement branchElement,
            ConditionalMarkerType markerType,
            string condition
        )
        {
            switch (markerType)
            {
                case ConditionalMarkerType.If:
                    branchElement.SetAttribute(VueIfAttribute, condition);
                    break;
                case ConditionalMarkerType.ElseIf:
                    branchElement.SetAttribute(VueElseIfAttribute, condition);
                    break;
                case ConditionalMarkerType.Else:
                    branchElement.SetAttribute(VueElseAttribute, string.Empty);
                    break;
            }
        }

        private static bool TryGetConditionalMarker(
            INode node,
            out ConditionalMarkerType markerType,
            out string condition
        )
        {
            markerType = ConditionalMarkerType.None;
            condition = string.Empty;
            if (node is not IElement { LocalName: "p" } paragraph || !IsHtmlElement(paragraph))
            {
                return false;
            }

            var markerElement = paragraph
                .QuerySelectorAll("span")
                .FirstOrDefault(element => HasClass(element, ConditionalMarkerClass));
            if (markerElement == null)
            {
                return false;
            }

            var markerText = markerElement.TextContent.Trim();
            if (markerText.Equals("#if", StringComparison.OrdinalIgnoreCase))
            {
                markerType = ConditionalMarkerType.If;
            }
            else if (markerText.Contains("#else if", StringComparison.OrdinalIgnoreCase))
            {
                markerType = ConditionalMarkerType.ElseIf;
            }
            else if (markerText.Equals("#else", StringComparison.OrdinalIgnoreCase))
            {
                markerType = ConditionalMarkerType.Else;
                return true;
            }
            else if (markerText.Contains("#end if", StringComparison.OrdinalIgnoreCase))
            {
                markerType = ConditionalMarkerType.EndIf;
                return true;
            }
            else
            {
                return false;
            }

            var equationElement = paragraph
                .QuerySelectorAll("span")
                .FirstOrDefault(element => HasClass(element, EquationClass));
            if (equationElement != null)
            {
                condition = ExtractCondition(equationElement);
            }

            return true;
        }

        private static string ExtractCondition(INode equationElement)
        {
            var conditionBuilder = new StringBuilder();
            AppendConditionText(equationElement, conditionBuilder);

            return string.Concat(
                conditionBuilder
                    .ToString()
                    .Replace("≡", "==", StringComparison.Ordinal)
                    .Replace("≠", "!=", StringComparison.Ordinal)
                    .Replace("≤", "<=", StringComparison.Ordinal)
                    .Replace("≥", ">=", StringComparison.Ordinal)
                    .Where(character => !char.IsWhiteSpace(character))
            );
        }

        private static void AppendConditionText(INode node, StringBuilder conditionBuilder)
        {
            foreach (var childNode in node.ChildNodes)
            {
                if (childNode.NodeType == NodeType.Text)
                {
                    conditionBuilder.Append(childNode.TextContent);
                    continue;
                }

                if (childNode is not IElement childElement)
                {
                    continue;
                }

                if (childElement.LocalName.Equals("sub", StringComparison.OrdinalIgnoreCase))
                {
                    var subscript = childElement.TextContent.Trim();
                    if (!string.IsNullOrEmpty(subscript))
                    {
                        conditionBuilder.Append('_');
                        conditionBuilder.Append(subscript);
                    }
                    continue;
                }

                if (childElement.LocalName.Equals("i", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AppendConditionText(childElement, conditionBuilder);
            }
        }

        private static bool RetainNodeRecursively(INode node)
        {
            if (node is not IElement element)
            {
                return false;
            }

            if (IsRetainedElement(element))
            {
                return true;
            }

            var containerType = GetContainerType(element);
            if (containerType == RetainedContainerType.Paragraph)
            {
                return ContainsSupportedElement(element);
            }

            if (containerType != RetainedContainerType.Conditional)
            {
                return false;
            }

            foreach (var childNode in element.ChildNodes.ToArray())
            {
                if (!RetainNodeRecursively(childNode))
                {
                    element.RemoveChild(childNode);
                }
            }

            return element.HasChildNodes;
        }

        private static RetainedContainerType GetContainerType(IElement element)
        {
            if (!IsHtmlElement(element))
            {
                return RetainedContainerType.None;
            }

            if (element.LocalName.Equals("p", StringComparison.OrdinalIgnoreCase))
            {
                return RetainedContainerType.Paragraph;
            }

            if (!element.LocalName.Equals("div", StringComparison.OrdinalIgnoreCase))
            {
                return RetainedContainerType.None;
            }

            var hasContainerClass =
                HasClass(element, ConditionalBlockClass) || HasClass(element, IndentClass);
            var hasConditionalAttribute =
                element.HasAttribute(VueIfAttribute)
                || element.HasAttribute(VueElseIfAttribute)
                || element.HasAttribute(VueElseAttribute);
            return hasContainerClass || hasConditionalAttribute
                ? RetainedContainerType.Conditional
                : RetainedContainerType.None;
        }

        private static bool IsRetainedElement(IElement element)
        {
            if (!IsHtmlElement(element))
            {
                return false;
            }

            return HeadingElementNames.Contains(element.LocalName)
                || SupportedElementNames.Contains(element.LocalName)
                || HasClass(element, ErrorClass);
        }

        private static bool ContainsSupportedElement(INode node)
        {
            if (node is IElement element && IsSupportedElement(element))
            {
                return true;
            }

            return node.ChildNodes.Any(ContainsSupportedElement);
        }

        private static bool IsSupportedElement(IElement element) =>
            IsHtmlElement(element) && SupportedElementNames.Contains(element.LocalName);

        private static bool IsHtmlElement(IElement element) =>
            string.Equals(element.NamespaceUri, HtmlNamespaceUri, StringComparison.Ordinal);

        private static bool HasClass(IElement element, string expectedClass) =>
            element.ClassList.Any(className =>
                className.Equals(expectedClass, StringComparison.OrdinalIgnoreCase)
            );

        private enum ConditionalMarkerType
        {
            None,
            If,
            ElseIf,
            Else,
            EndIf,
        }

        private enum RetainedContainerType
        {
            None,
            Paragraph,
            Conditional,
        }
    }
}
