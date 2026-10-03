using System;
using NUnit.Framework;
using ResourceLoader.Editor;
using UnityEngine;

namespace ResourceLoader.Tests
{
public sealed class AddressCodeGeneratorTests
{
    private AddressGeneratorSettings _settings;

    [SetUp]
    public void SetUp()
    {
        _settings = ScriptableObject.CreateInstance<AddressGeneratorSettings>();
        _settings.generatedNamespace = "Game.Generated";
        _settings.className = "ResourceIds";
        _settings.separators.Clear();
        _settings.separators.Add("/");
        _settings.separators.Add("-");
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(_settings);

    [Test]
    public void Generate_GroupsByGroupAndConfiguredSeparators_ValuesKeepOriginalAddresses()
    {
        var result = AddressCodeGenerator.Generate(new[]
        {
            new AddressCodeGenerator.Record("UI", "Inventory/Sword-Icon")
        }, _settings);

        Assert.That(result, Does.Contain("public static class UI"));
        Assert.That(result, Does.Contain("public static class Inventory"));
        Assert.That(result, Does.Contain("public static class Sword"));
        Assert.That(result, Does.Contain("public const string Icon = \"Inventory/Sword-Icon\";"));
    }

    [Test]
    public void Generate_ConflictingPaths_ReportsBothAddresses()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => AddressCodeGenerator.Generate(new[]
        {
            new AddressCodeGenerator.Record("UI", "Shop/Buy"),
            new AddressCodeGenerator.Record("UI", "Shop/Buy")
        }, _settings));
        Assert.That(ex.Message, Does.Contain("Shop/Buy"));
    }

    [Test]
    public void Generate_IsStableRegardlessOfInputOrder()
    {
        var first = new AddressCodeGenerator.Record("UI", "B");
        var second = new AddressCodeGenerator.Record("UI", "A");
        var ordered = AddressCodeGenerator.Generate(new[] { first, second }, _settings);
        var reversed = AddressCodeGenerator.Generate(new[] { second, first }, _settings);
        Assert.That(ordered, Is.EqualTo(reversed));
    }

    [Test]
    public void Generate_EscapesLiteralWithoutChangingAddress()
    {
        var result = AddressCodeGenerator.Generate(new[]
        {
            new AddressCodeGenerator.Record("UI", "Quote\"Here")
        }, _settings);
        Assert.That(result, Does.Contain("= \"Quote\\\"Here\";"));
    }

    [Test]
    public void Generate_WithoutGroupPrefix_OmitsGroupContainer()
    {
        _settings.includeGroupName = false;
        var result = AddressCodeGenerator.Generate(new[]
        {
            new AddressCodeGenerator.Record("UI", "Inventory/Icon")
        }, _settings);
        Assert.That(result, Does.Not.Contain("public static class UI"));
        Assert.That(result, Does.Contain("public static class Inventory"));
    }

    [Test]
    public void Generate_ConflictingLeafAndGroup_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => AddressCodeGenerator.Generate(new[]
        {
            new AddressCodeGenerator.Record("UI", "Inventory"),
            new AddressCodeGenerator.Record("UI", "Inventory/Icon")
        }, _settings));
    }

    [Test]
    public void Generate_KeywordIdentifier_IsEscaped()
    {
        _settings.pascalCaseNames = false;
        var result = AddressCodeGenerator.Generate(new[]
        {
            new AddressCodeGenerator.Record("UI", "class")
        }, _settings);
        Assert.That(result, Does.Contain("public const string @class = \"class\";"));
    }
}
}
