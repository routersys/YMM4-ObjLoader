using ObjLoader.Utilities;
using System.IO;
using System.Text;

namespace ObjLoader.Rendering.Shaders.Fx;

internal sealed class FxPreprocessor
{
    private readonly Dictionary<string, string> _defines = new(StringComparer.Ordinal);
    private readonly HashSet<string> _resolvedIncludes = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxIncludeDepth = 16;

    public string Process(string source, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(baseDirectory);

        _defines.Clear();
        _resolvedIncludes.Clear();

        return ProcessSource(source, baseDirectory, 0);
    }

    private string ProcessSource(string source, string baseDirectory, int depth)
    {
        if (depth > MaxIncludeDepth)
        {
            return source;
        }

        var lines = source.Split('\n');
        var output = new StringBuilder(source.Length);
        var ifStack = new Stack<IfState>();

        for (var i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("#define", StringComparison.Ordinal))
            {
                if (IsActive(ifStack))
                {
                    ProcessDefine(trimmed);
                }
                continue;
            }

            if (trimmed.StartsWith("#undef", StringComparison.Ordinal))
            {
                if (IsActive(ifStack))
                {
                    ProcessUndef(trimmed);
                }
                continue;
            }

            if (trimmed.StartsWith("#ifdef", StringComparison.Ordinal))
            {
                var macroName = trimmed[6..].Trim();
                ifStack.Push(IsActive(ifStack) && _defines.ContainsKey(macroName)
                    ? IfState.Active
                    : IfState.Skipping);
                continue;
            }

            if (trimmed.StartsWith("#ifndef", StringComparison.Ordinal))
            {
                var macroName = trimmed[7..].Trim();
                ifStack.Push(IsActive(ifStack) && !_defines.ContainsKey(macroName)
                    ? IfState.Active
                    : IfState.Skipping);
                continue;
            }

            if (trimmed.StartsWith("#if ", StringComparison.Ordinal) ||
                string.Equals(trimmed, "#if", StringComparison.Ordinal))
            {
                if (IsActive(ifStack))
                {
                    var expr = trimmed.Length > 3 ? trimmed[3..].Trim() : "0";
                    ifStack.Push(EvaluateConditionExpression(expr) ? IfState.Active : IfState.Skipping);
                }
                else
                {
                    ifStack.Push(IfState.ParentSkipping);
                }
                continue;
            }

            if (trimmed.StartsWith("#elif", StringComparison.Ordinal))
            {
                if (ifStack.Count > 0)
                {
                    var current = ifStack.Pop();
                    if (current == IfState.Skipping && IsActive(ifStack))
                    {
                        var expr = trimmed.Length > 5 ? trimmed[5..].Trim() : "0";
                        ifStack.Push(EvaluateConditionExpression(expr) ? IfState.Active : IfState.Skipping);
                    }
                    else if (current == IfState.Active)
                    {
                        ifStack.Push(IfState.AlreadyMatched);
                    }
                    else
                    {
                        ifStack.Push(current);
                    }
                }
                continue;
            }

            if (string.Equals(trimmed, "#else", StringComparison.Ordinal))
            {
                if (ifStack.Count > 0)
                {
                    var current = ifStack.Pop();
                    ifStack.Push(current == IfState.Skipping ? IfState.Active : IfState.AlreadyMatched);
                }
                continue;
            }

            if (string.Equals(trimmed, "#endif", StringComparison.Ordinal))
            {
                if (ifStack.Count > 0)
                {
                    ifStack.Pop();
                }
                continue;
            }

            if (!IsActive(ifStack))
            {
                continue;
            }

            if (trimmed.StartsWith("#include", StringComparison.Ordinal))
            {
                var included = ProcessInclude(trimmed, baseDirectory, depth);
                if (included is not null)
                {
                    output.AppendLine(included);
                }
                continue;
            }

            if (trimmed.StartsWith("#pragma", StringComparison.Ordinal) ||
                trimmed.StartsWith("#error", StringComparison.Ordinal) ||
                trimmed.StartsWith("#warning", StringComparison.Ordinal))
            {
                continue;
            }

            output.AppendLine(ExpandMacros(line));
        }

        return output.ToString();
    }

    private static bool IsActive(Stack<IfState> stack)
    {
        return stack.Count == 0 || stack.Peek() == IfState.Active;
    }

    private void ProcessDefine(string trimmed)
    {
        var rest = trimmed[7..].TrimStart();
        var spaceIdx = rest.IndexOfAny(new[] { ' ', '\t' });

        if (spaceIdx < 0)
        {
            _defines[rest] = "1";
            return;
        }

        var macroName = rest[..spaceIdx];
        var macroValue = rest[spaceIdx..].Trim();
        _defines[macroName] = macroValue;
    }

    private void ProcessUndef(string trimmed)
    {
        var macroName = trimmed[6..].Trim();
        _defines.Remove(macroName);
    }

    private bool EvaluateConditionExpression(string expr)
    {
        expr = ExpandMacros(expr);
        expr = expr.Trim();

        if (TryEvaluateComparison(expr, out var compResult))
        {
            return compResult;
        }

        if (int.TryParse(expr, out var intVal))
        {
            return intVal != 0;
        }

        if (expr.StartsWith("defined(", StringComparison.Ordinal) && expr.EndsWith(')'))
        {
            var name = expr[8..^1].Trim();
            return _defines.ContainsKey(name);
        }

        if (expr.StartsWith("!", StringComparison.Ordinal))
        {
            return !EvaluateConditionExpression(expr[1..].Trim());
        }

        return false;
    }

    private static bool TryEvaluateComparison(string expr, out bool result)
    {
        result = false;

        string[] operators = ["==", "!=", ">=", "<=", ">", "<"];
        foreach (var op in operators)
        {
            var idx = expr.IndexOf(op, StringComparison.Ordinal);
            if (idx < 0) continue;

            var left = expr[..idx].Trim();
            var right = expr[(idx + op.Length)..].Trim();

            if (!int.TryParse(left, out var lv) || !int.TryParse(right, out var rv)) continue;

            result = op switch
            {
                "==" => lv == rv,
                "!=" => lv != rv,
                ">=" => lv >= rv,
                "<=" => lv <= rv,
                ">" => lv > rv,
                "<" => lv < rv,
                _ => false
            };
            return true;
        }

        return false;
    }

    private string ExpandMacros(string text)
    {
        foreach (var (name, value) in _defines)
        {
            text = ReplaceWholeWord(text, name, value);
        }
        return text;
    }

    private static string ReplaceWholeWord(string text, string word, string replacement)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var idx = text.IndexOf(word, i, StringComparison.Ordinal);
            if (idx < 0)
            {
                sb.Append(text, i, text.Length - i);
                break;
            }

            var before = idx > 0 ? text[idx - 1] : ' ';
            var after = idx + word.Length < text.Length ? text[idx + word.Length] : ' ';
            var isWordBoundaryBefore = !char.IsLetterOrDigit(before) && before != '_';
            var isWordBoundaryAfter = !char.IsLetterOrDigit(after) && after != '_';

            sb.Append(text, i, idx - i);
            if (isWordBoundaryBefore && isWordBoundaryAfter)
            {
                sb.Append(replacement);
            }
            else
            {
                sb.Append(word);
            }
            i = idx + word.Length;
        }
        return sb.ToString();
    }

    private string? ProcessInclude(string trimmed, string baseDirectory, int depth)
    {
        var start = trimmed.IndexOfAny(new[] { '"', '<' });
        if (start < 0) return null;

        var endChar = trimmed[start] == '"' ? '"' : '>';
        var end = trimmed.IndexOf(endChar, start + 1);
        if (end < 0) return null;

        var includePath = trimmed[(start + 1)..end];
        var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, includePath));

        if (_resolvedIncludes.Contains(fullPath)) return null;
        if (!File.Exists(fullPath)) return null;

        _resolvedIncludes.Add(fullPath);

        var includeSource = EncodingUtil.ReadAllText(fullPath);
        var includeDir = Path.GetDirectoryName(fullPath) ?? baseDirectory;
        return ProcessSource(includeSource, includeDir, depth + 1);
    }

    public IReadOnlyDictionary<string, string> GetDefines() => _defines;

    private enum IfState
    {
        Active,
        Skipping,
        AlreadyMatched,
        ParentSkipping
    }
}