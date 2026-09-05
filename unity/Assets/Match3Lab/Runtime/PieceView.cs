using Match3Lab.Core;
using UnityEngine;

namespace Match3Lab.Unity
{
    /// <summary>One piece on screen. Pooled by <see cref="BoardView"/>; knows nothing about rules.</summary>
    public sealed class PieceView : MonoBehaviour
    {
        public const int SortingOrder = 20;

        private SpriteRenderer _renderer;

        public Piece Piece { get; private set; }

        public static PieceView Create(Transform parent)
        {
            var go = new GameObject("Piece");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<PieceView>();
            view._renderer = go.AddComponent<SpriteRenderer>();
            view._renderer.sortingOrder = SortingOrder;
            return view;
        }

        public void Show(Piece piece, SpriteFactory sprites)
        {
            Piece = piece;
            _renderer.sprite = sprites.For(piece);
            _renderer.color = SpriteFactory.ColorOf(piece);
            transform.localRotation = piece.Type == PieceType.RocketH ? Quaternion.Euler(0, 0, -90) : Quaternion.identity;
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);
        }

        public void SetAlpha(float a)
        {
            var c = _renderer.color;
            c.a = a;
            _renderer.color = c;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
