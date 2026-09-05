using System;
using System.Collections.Generic;
using Match3Lab.Core;
using UnityEngine;

namespace Match3Lab.Unity
{
    /// <summary>
    /// The levels shipped with the build, loaded from <c>Resources/Levels</c>. The editor's
    /// "Sync Levels" step copies them there from the repository's <c>levels/</c> folder, which
    /// stays the single source the CLI and the simulator also read.
    /// </summary>
    public static class LevelCatalog
    {
        public static List<(string name, TextAsset asset)> Load()
        {
            var assets = Resources.LoadAll<TextAsset>("Levels");
            var list = new List<(string, TextAsset)>(assets.Length);
            foreach (var a in assets) list.Add((a.name, a));
            list.Sort((x, y) => string.CompareOrdinal(x.Item1, y.Item1));
            return list;
        }

        public static LevelDefinition Parse(TextAsset asset)
        {
            try
            {
                return LevelText.Parse(asset.text);
            }
            catch (LevelFormatException ex)
            {
                Debug.LogError("Level '" + asset.name + "' is invalid: " + ex.Message);
                throw;
            }
        }

        /// <summary>A fallback so the scene runs even before any level has been synced.</summary>
        public static LevelDefinition Builtin()
        {
            return LevelText.Parse(
                "name Built-in\nsize 7 7\nmoves 15\ncolors 4\ngoal color 0 20\ngrid\n" +
                string.Concat(new string[] { ". . . . . . .\n", ". . . . . . .\n", ". . . . . . .\n", ". . . . . . .\n", ". . . . . . .\n", ". . . . . . .\n", ". . . . . . .\n" }));
        }
    }
}
