using System.IO;
using UnityEngine;

namespace Match3Lab.UnityEditor
{
    /// <summary>Where the repository keeps things, relative to the Unity project.</summary>
    public static class RepoPaths
    {
        public static string LevelsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "levels"));

        public static string[] LevelFiles()
        {
            if (!Directory.Exists(LevelsDir)) return new string[0];
            var files = Directory.GetFiles(LevelsDir, "*.txt");
            System.Array.Sort(files, System.StringComparer.Ordinal);
            return files;
        }
    }
}
