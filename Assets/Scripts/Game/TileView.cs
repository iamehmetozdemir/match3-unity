using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>The on-screen object for one tile. Pure presentation: all rules live in Match3.Core.</summary>
    public sealed class TileView : MonoBehaviour
    {
        private const float BaseScale = 0.92f;

        private SpriteRenderer _body;
        private SpriteRenderer _overlay;

        public Tile Tile { get; private set; }
        public Vec2I Cell { get; set; }
        public SpriteRenderer Body => _body;

        public static TileView Create(Transform parent)
        {
            var go = new GameObject("Tile");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<TileView>();

            view._body = go.AddComponent<SpriteRenderer>();
            view._body.sortingOrder = 10;

            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(go.transform, false);
            view._overlay = overlay.AddComponent<SpriteRenderer>();
            view._overlay.sortingOrder = 11;
            view.Scale = 1f;
            return view;
        }

        /// <summary>Visual scale multiplier (1 = normal size).</summary>
        public float Scale
        {
            set { transform.localScale = Vector3.one * (BaseScale * value); }
        }

        public void SetTile(Tile tile)
        {
            Tile = tile;
            if (tile.Special == SpecialType.ColorBomb)
            {
                _body.sprite = SpriteFactory.ColorBomb();
                _overlay.enabled = false;
                return;
            }

            _body.sprite = SpriteFactory.Gem(tile.Color);
            _overlay.enabled = tile.IsSpecial;
            if (tile.IsSpecial) _overlay.sprite = SpriteFactory.Overlay(tile.Special);
        }

        public void SetSortingBoost(int boost)
        {
            _body.sortingOrder = 10 + boost;
            _overlay.sortingOrder = 11 + boost;
        }
    }
}
