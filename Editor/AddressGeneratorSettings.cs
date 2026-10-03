using System.Collections.Generic;
using UnityEngine;

namespace ResourceLoader.Editor
{
[CreateAssetMenu(menuName = "ResourceLoader/Address Generator Settings")]
public sealed class AddressGeneratorSettings : ScriptableObject
{
    public string outputPath = "Assets/Generated/ResourceIds.cs";
    public string generatedNamespace = "Game.Generated";
    public string className = "ResourceIds";
    public List<string> separators = new List<string> { "/" };
    public bool includeGroupName = true;
    public bool pascalCaseNames = true;
    public bool includeAllGroups = true;
    public List<string> includedGroupNames = new List<string>();
}
}
