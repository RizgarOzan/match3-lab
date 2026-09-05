using Match3Lab.Core;
using UnityEngine;

namespace Match3Lab.Unity
{
    /// <summary>The static part of a cell: floor tile, grass under, ice over, or a box.</summary>
    public sealed class CellView : MonoBehaviour
    {
        private static readonly Color FloorA = new Color(0.16f, 0.18f, 0.23f);
        private static readonly Color FloorB = new Color(0.19f, 0.21f, 0.27f);
        private static readonly Color GrassOne = new Color(0.36f, 0.66f, 0.34f, 0.85f);
        private static readonly Color GrassTwo = new Color(0.22f, 0.50f, 0.24f, 0.95f);
        private static readonly Color IceOne = new Color(0.70f, 0.88f, 1.00f, 0.55f);
        private static readonly Color IceTwo = new Color(0.55f, 0.78f, 1.00f, 0.80f);
        private static readonly Color BoxOne = new Color(0.62f, 0.45f, 0.28f);
        private static readonly Color BoxTwo = new Color(0.45f, 0.30f, 0.18f);

        private SpriteRenderer _floor;
        private SpriteRenderer _grass;
        private SpriteRenderer _box;
        private SpriteRenderer _ice;

        public static CellView Create(Transform parent, SpriteFactory sprites, GridPos pos)
        {
            var go = new GameObject("Cell " + pos);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CellView>();
            view._floor = Layer(go.transform, sprites.Tile, 0);
            view._floor.color = (pos.X + pos.Y) % 2 == 0 ? FloorA : FloorB;
            view._grass = Layer(go.transform, sprites.Square, 5);
            view._grass.transform.localScale = Vector3.one * 0.92f;
            view._box = Layer(go.transform, sprites.Gem, 20);
            view._ice = Layer(go.transform, sprites.Square, 30);
            view._ice.transform.localScale = Vector3.one * 0.92f;
            return view;
        }

        private static SpriteRenderer Layer(Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject("layer" + order);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        public void Show(in Cell cell)
        {
            if (cell.Hole)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);
            _grass.enabled = cell.Grass > 0;
            _grass.color = cell.Grass >= 2 ? GrassTwo : GrassOne;
            _box.enabled = cell.Box > 0;
            _box.color = cell.Box >= 2 ? BoxTwo : BoxOne;
            _ice.enabled = cell.Ice > 0;
            _ice.color = cell.Ice >= 2 ? IceTwo : IceOne;
        }
    }
}
