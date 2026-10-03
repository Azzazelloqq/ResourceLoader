using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace ResourceLoader.Editor
{
public static class AddressCodeGenerator
{
    private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
        "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte",
        "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
        "ushort", "using", "virtual", "void", "volatile", "while"
    };
    public readonly struct Record
    {
        public readonly string Group;
        public readonly string Address;
        public Record(string group, string address) { Group = group; Address = address; }
    }

    private sealed class Node
    {
        public readonly SortedDictionary<string, Node> Children =
            new SortedDictionary<string, Node>(StringComparer.Ordinal);
        public string Address;
    }

    public static IReadOnlyList<Record> ReadAddresses(AddressGeneratorSettings config)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables settings are unavailable.");
        var records = new List<Record>();
        var foundGroups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            if (!config.includeAllGroups && !config.includedGroupNames.Contains(group.Name)) continue;
            foundGroups.Add(group.Name);
            var entries = new List<AddressableAssetEntry>();
            group.GatherAllAssets(entries, true, true, false);
            foreach (var entry in entries)
                if (entry != null && !entry.IsFolder) records.Add(new Record(group.Name, entry.address));
        }
        if (!config.includeAllGroups)
            foreach (var groupName in config.includedGroupNames)
                if (!foundGroups.Contains(groupName))
                    throw new InvalidOperationException($"Selected Addressables group '{groupName}' does not exist.");
        return records;
    }

    public static string Generate(IEnumerable<Record> records, AddressGeneratorSettings config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        var rootName = Identifier(config.className, config.pascalCaseNames);
        if (string.IsNullOrWhiteSpace(config.generatedNamespace))
            throw new InvalidOperationException("Namespace cannot be empty.");
        foreach (var part in config.generatedNamespace.Split('.'))
            if (Identifier(part, false) != part)
                throw new InvalidOperationException($"Invalid namespace segment '{part}'.");
        var separators = config.separators.Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.Ordinal).OrderByDescending(s => s.Length).ToArray();
        if (separators.Length == 0) throw new InvalidOperationException("At least one separator is required.");

        var root = new Node();
        foreach (var record in records.OrderBy(r => r.Group, StringComparer.Ordinal)
                     .ThenBy(r => r.Address, StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(record.Address))
                throw new InvalidOperationException($"Group '{record.Group}' contains an empty address.");
            var parts = Split(record.Address, separators);
            if (config.includeGroupName) parts.Insert(0, record.Group);
            if (parts.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException($"Address '{record.Address}' has an empty path segment.");
            var node = root;
            for (var i = 0; i < parts.Count; i++)
            {
                var name = Identifier(parts[i], config.pascalCaseNames);
                if (name == rootName && node == root)
                    throw new InvalidOperationException($"Generated member '{name}' conflicts with root class '{rootName}'.");
                if (node.Address != null)
                    throw new InvalidOperationException($"Address '{node.Address}' conflicts with nested address '{record.Address}'.");
                if (!node.Children.TryGetValue(name, out var child))
                    node.Children.Add(name, child = new Node());
                node = child;
            }
            if (node.Address != null || node.Children.Count != 0)
                throw new InvalidOperationException($"Generated path for '{record.Address}' conflicts with '{node.Address ?? "a group"}'.");
            node.Address = record.Address;
        }
        var code = new StringBuilder("// <auto-generated />\n");
        code.Append("namespace ").Append(config.generatedNamespace).Append("\n{\n");
        code.Append("    public static class ").Append(rootName).Append("\n    {\n");
        WriteChildren(code, root, 2);
        code.Append("    }\n}\n");
        return code.ToString();
    }

    private static List<string> Split(string address, string[] separators)
    {
        var parts = new List<string>();
        var start = 0;
        for (var i = 0; i < address.Length;)
        {
            var separator = separators.FirstOrDefault(s => i + s.Length <= address.Length &&
                string.CompareOrdinal(address, i, s, 0, s.Length) == 0);
            if (separator == null) { i++; continue; }
            parts.Add(address.Substring(start, i - start));
            i += separator.Length;
            start = i;
        }
        parts.Add(address.Substring(start));
        return parts;
    }

    private static string Identifier(string value, bool pascalCase)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("An identifier is empty.");
        var result = new StringBuilder();
        var capitalize = pascalCase;
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                if (c == '_' && pascalCase) { capitalize = true; continue; }
                result.Append(capitalize ? char.ToUpperInvariant(c) : c);
                capitalize = false;
            }
            else capitalize = true;
        }
        if (result.Length == 0) throw new InvalidOperationException($"'{value}' contains no valid identifier characters.");
        if (char.IsDigit(result[0])) result.Insert(0, '_');
        var name = result.ToString();
        if (Keywords.Contains(name)) name = "@" + name;
        return name;
    }

    private static void WriteChildren(StringBuilder code, Node node, int level)
    {
        foreach (var item in node.Children)
        {
            var padding = new string(' ', level * 4);
            if (item.Value.Address != null)
                code.Append(padding).Append("public const string ").Append(item.Key).Append(" = \"")
                    .Append(Escape(item.Value.Address)).Append("\";\n");
            else
            {
                code.Append(padding).Append("public static class ").Append(item.Key).Append("\n")
                    .Append(padding).Append("{\n");
                WriteChildren(code, item.Value, level + 1);
                code.Append(padding).Append("}\n");
            }
        }
    }

    private static string Escape(string value)
    {
        var result = new StringBuilder();
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': result.Append("\\\\"); break;
                case '"': result.Append("\\\""); break;
                case '\n': result.Append("\\n"); break;
                case '\r': result.Append("\\r"); break;
                case '\t': result.Append("\\t"); break;
                default:
                    if (char.IsControl(c)) result.Append("\\u").Append(((int)c).ToString("x4"));
                    else result.Append(c);
                    break;
            }
        }
        return result.ToString();
    }
}
}
