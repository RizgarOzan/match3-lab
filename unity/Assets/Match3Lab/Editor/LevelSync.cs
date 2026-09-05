using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Match3Lab.UnityEditor
{
    /// <summary>
    /// Copies <c>levels/*.txt</c> from the repository root into <c>Assets/Match3Lab/Resources/Levels</c>.
    /// Runs from the menu and automatically before every build, so the shipped levels can never
    /// drift from the ones the CLI and the simulator measured.
    /// </summary>
    public sealed class LevelSync : IPreprocessBuildWithReport
    {
        public const string SourceDir = "../levels";
        public const string TargetDir = "Assets/Match3Lab/Resources/Levels";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Sync();

        [MenuItem("Match3 Lab/Sync Levels From Repo")]
        public static void Sync()
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SourceDir));
            if (!Directory.Exists(source))
            {
                Debug.LogWarning("Level sync: source folder not found: " + source);
                return;
            }
            Directory.CreateDirectory(TargetDir);
            foreach (var old in Directory.GetFiles(TargetDir, "*.txt")) File.Delete(old);
            int n = 0;
            foreach (var file in Directory.GetFiles(source, "*.txt"))
            {
                File.Copy(file, Path.Combine(TargetDir, Path.GetFileName(file)), true);
                n++;
            }
            AssetDatabase.Refresh();
            Debug.Log("Level sync: copied " + n + " level(s) from " + source);
        }
    }
}
