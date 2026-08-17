using System;
using System.Collections.Generic;
using System.Text;
using Calcpad.Core;
using Calcpad.Document.Core.Segments.Input;

namespace Calcpad.Document.Core.Segments
{
    public class InputLine : CpdLine
    {
        // 搜索 #include 指令
        private static readonly System.Buffers.SearchValues<char> s_includeSearchValues =
            System.Buffers.SearchValues.Create("#include");

        private readonly string _originalLine;
        public List<InputField> Fields { get; private set; } = [];

        public bool ContainsInputs => Fields.Count > 0;

        public bool HasUpdatedFields => Fields.Any(x => x.IsUpdated);

        public InputLine(uint rowIndex, string line)
            : base(rowIndex)
        {
            _originalLine = line ?? string.Empty;

            // resolve input fields from the line
            if (string.IsNullOrEmpty(line))
                return;

            ExtractLineInputFields(line);
        }

        #region 提取变量值
        /// <summary>
        /// extract input fields from a line
        /// </summary>
        /// <param name="s"></param>
        /// <param name="values"></param>
        private void ExtractLineInputFields(ReadOnlySpan<char> s)
        {
            Fields.Clear();

            if (s.Length == 0)
                return;

            // handle #include first as #{val1;val2;}
            var trimmed = s.TrimEnd();
            var lastHashIndex = trimmed.LastIndexOf('#');
            var lastLeftBrace = trimmed.LastIndexOf('{');
            var lastRightBrace = trimmed.LastIndexOf('}');
            if (
                lastHashIndex > 0
                && lastHashIndex + 1 == lastLeftBrace
                && lastLeftBrace < lastRightBrace
            )
            {
                var valuesStr = trimmed[(lastLeftBrace + 1)..lastRightBrace];
                var values = valuesStr.ToString().Split(';');
                var name = trimmed[..lastHashIndex].ToString().Trim();
                AddInputField(
                    new InputField(values, name)
                    {
                        Type = InputFieldType.Include,
                        ValueStartIndex = lastLeftBrace + 1,
                        ValueEndIndex = lastRightBrace
                    }
                );
                return;
            }

            // 处理注释中的 variable = ? {val}
            var commentEnumerator = s.EnumerateComments();
            var itemStartIndex = 0;
            // 非 include 时处理
            foreach (var item in commentEnumerator)
            {
                if (!item.IsEmpty && item[0] != '"' && item[0] != '\'')
                {
                    var inputChar = '\0';
                    var braceStart = -1;
                    for (int j = 0; j < item.Length; ++j)
                    {
                        var c = item[j];
                        if (c == '?')
                        {
                            inputChar = c;
                        }
                        else if (c == '{' && inputChar == '?')
                        {
                            inputChar = '{';
                            braceStart = j + 1;
                        }
                        else if (c == '}' && inputChar == '{')
                        {
                            var val = item[braceStart..j].ToString().Trim();
                            var equalIndex = item.IndexOf('=');

                            var name = item[..(equalIndex - 1)].ToString().Trim();
                            AddInputField(
                                new InputField([val], name)
                                {
                                    Type = InputFieldType.Variable,
                                    ValueStartIndex = itemStartIndex + braceStart,
                                    ValueEndIndex = itemStartIndex + j
                                }
                            );
                            inputChar = '\0';
                            braceStart = -1;
                        }
                    }
                }

                itemStartIndex += item.Length;
            }
        }

        private void AddInputField(InputField field)
        {
            Fields.Add(field);
        }
        #endregion


        public override string ToString()
        {
            if (!HasUpdatedFields)
                return _originalLine;

            var updatedFields = Fields
                .Where(x => x.IsUpdated && x.HasValueRange)
                .OrderBy(x => x.ValueStartIndex)
                .ToArray();

            if (updatedFields.Length == 0)
                return _originalLine;

            var sb = new StringBuilder(_originalLine.Length + updatedFields.Length * 20);
            var lastIndex = 0;
            foreach (var field in updatedFields)
            {
                if (field.ValueStartIndex < lastIndex || field.ValueEndIndex > _originalLine.Length)
                    return _originalLine;

                sb.Append(_originalLine.AsSpan(lastIndex, field.ValueStartIndex - lastIndex));
                sb.Append(string.Join(';', field.Values));
                lastIndex = field.ValueEndIndex;
            }

            sb.Append(_originalLine.AsSpan(lastIndex));
            return sb.ToString();
        }

        public static bool IsInputLine(ReadOnlySpan<char> line)
        {
            // check for ? {...} pattern
            var index = line.IndexOf('?');
            if (index >= 0 && index + 2 < line.Length && line[index + 2] == '{')
                return true;

            // check for #include directive
            if (line.Contains(IncludeLine.IncludeDirective, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }
}
