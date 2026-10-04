using System;
using ResourceLoader.Editor;
using UnityEditor;
using UnityEngine;

namespace ResourceLoader.Examples.Editor
{
public static class AddressGeneratorPreviewExample
{
    [MenuItem("Tools/ResourceLoader/Examples/Preview Selected Address Settings")]
    private static void PreviewSelectedSettings()
    {
        var settings = Selection.activeObject as AddressGeneratorSettings;
        if (settings == null)
        {
            Debug.LogWarning("Select an Address Generator Settings asset first.");
            return;
        }

        try
        {
            // Read actual Addressables entries, then preview the generated C# text.
            // This example does not write a generated file or define resource IDs.
            var records = AddressCodeGenerator.ReadAddresses(settings);
            Debug.Log(AddressCodeGenerator.Generate(records, settings));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
}
