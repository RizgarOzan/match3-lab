using System.Collections;
using Match3Lab.Core;
using Match3Lab.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Match3Lab.Tests
{
    /// <summary>
    /// Runs the real demo scene in play mode and lets the bot play. Proves the wiring — scene,
    /// controller, sprite generation, HUD, event replay — without a human at the keyboard, and
    /// runs headless in batch mode so it can gate a build.
    /// </summary>
    public class DemoSceneSmokeTests
    {
        private const string ScenePath = "Assets/Match3Lab/Scenes/Demo.unity";

        [UnityTest]
        public IEnumerator Bot_plays_several_moves_on_the_first_level_without_errors()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Demo", LoadSceneMode.Single);
#endif
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller, "GameController not found in the demo scene");
            Assert.IsNotNull(controller.Current, "no game was started");
            Assert.AreEqual(GameStatus.Playing, controller.Current.Status);

            int before = controller.Current.MovesPlayed;
            yield return controller.PlayMovesWithBot(4);

            Assert.GreaterOrEqual(controller.Current.MovesPlayed - before, 1, "the bot did not play");
            Assert.IsFalse(controller.IsBusy, "controller still busy after the moves finished");

            // Every playable cell must hold a piece once the board is quiet.
            var board = controller.Current.Board;
            foreach (var p in board.Positions())
            {
                if (board[p].IsPlayable) Assert.IsFalse(board[p].Piece.IsEmpty, "empty playable cell at " + p);
            }
        }

        [UnityTest]
        public IEnumerator Loading_every_shipped_level_starts_a_playable_game()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Demo", LoadSceneMode.Single);
#endif
            yield return null;
            var controller = Object.FindFirstObjectByType<GameController>();
            int count = Mathf.Max(1, controller.LevelCount);
            for (int i = 0; i < count; i++)
            {
                controller.LoadLevel(i);
                yield return null;
                Assert.AreEqual(GameStatus.Playing, controller.Current.Status, "level " + i);
                Assert.Greater(controller.Current.LegalMoves().Count, 0, "level " + i + " has no legal move");
            }
        }
    }
}
