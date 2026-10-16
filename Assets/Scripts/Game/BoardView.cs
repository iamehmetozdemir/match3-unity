using System;
using System.Collections;
using System.Collections.Generic;
using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Shows a <see cref="Board"/> and plays back a <see cref="TurnResult"/> as animation.
    /// The core already computed the outcome; this class only decides how it looks.
    /// Tile positions are local, so the whole board can be shaken by moving this transform.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private static readonly Color CellDark = new Color(0.15f, 0.17f, 0.27f);
        private static readonly Color CellLight = new Color(0.19f, 0.21f, 0.32f);
        private static readonly Color Frame = new Color(0.09f, 0.10f, 0.17f);

        private TileView[,] _views;
        private int _width;
        private int _height;
        private SpriteRenderer _selection;
        private Coroutine _hint;
        private readonly List<TileView> _hinted = new List<TileView>();

        public int Width => _width;
        public int Height => _height;

        /// <summary>Number of tile objects currently shown (should equal the number of filled board cells).</summary>
        public int TileCount
        {
            get
            {
                int n = 0;
                if (_views == null) return 0;
                foreach (TileView v in _views) if (v != null) n++;
                return n;
            }
        }

        // ---------- setup ----------

        public void Build(Board board)
        {
            StopAllCoroutines();
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            transform.position = Vector3.zero;
            _hint = null;
            _hinted.Clear();

            _width = board.Width;
            _height = board.Height;
            _views = new TileView[_width, _height];

            CreateBackground();

            var selectionObject = new GameObject("Selection");
            selectionObject.transform.SetParent(transform, false);
            _selection = selectionObject.AddComponent<SpriteRenderer>();
            _selection.sprite = SpriteFactory.Square();
            _selection.color = new Color(1f, 1f, 1f, 0.35f);
            _selection.sortingOrder = 5;
            _selection.enabled = false;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Tile tile = board[x, y];
                    if (tile.IsEmpty) continue;
                    TileView view = NewView(tile, new Vec2I(x, y));
                    view.transform.localPosition = CellToLocal(x, y);
                    _views[x, y] = view;
                }
            }
        }

        private void CreateBackground()
        {
            var frame = new GameObject("Frame");
            frame.transform.SetParent(transform, false);
            var frameRenderer = frame.AddComponent<SpriteRenderer>();
            frameRenderer.sprite = SpriteFactory.Square();
            frameRenderer.color = Frame;
            frameRenderer.sortingOrder = -2;
            frame.transform.localPosition = new Vector3((_width - 1) / 2f, (_height - 1) / 2f, 0);
            frame.transform.localScale = new Vector3(_width + 0.5f, _height + 0.5f, 1f);

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var cell = new GameObject("Cell");
                    cell.transform.SetParent(transform, false);
                    var r = cell.AddComponent<SpriteRenderer>();
                    r.sprite = SpriteFactory.Square();
                    r.color = (x + y) % 2 == 0 ? CellDark : CellLight;
                    r.sortingOrder = -1;
                    cell.transform.localPosition = CellToLocal(x, y);
                    cell.transform.localScale = Vector3.one * 0.96f;
                }
            }
        }

        private TileView NewView(Tile tile, Vec2I cell)
        {
            TileView view = TileView.Create(transform);
            view.SetTile(tile);
            view.Cell = cell;
            return view;
        }

        public static Vector3 CellToLocal(int x, int y) => new Vector3(x, y, 0f);

        public bool TryGetCell(Vector3 worldPoint, out Vec2I cell)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            int x = Mathf.RoundToInt(local.x);
            int y = Mathf.RoundToInt(local.y);
            cell = new Vec2I(x, y);
            return x >= 0 && y >= 0 && x < _width && y < _height
                   && Mathf.Abs(local.x - x) <= 0.5f && Mathf.Abs(local.y - y) <= 0.5f;
        }

        public void ShowSelection(Vec2I? cell)
        {
            if (_selection == null) return;
            _selection.enabled = cell.HasValue;
            if (cell.HasValue) _selection.transform.localPosition = CellToLocal(cell.Value.X, cell.Value.Y);
        }

        /// <summary>Makes the views match the board exactly. Used as a safety net after every turn.</summary>
        public void Resync(Board board)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Tile tile = board[x, y];
                    TileView view = _views[x, y];
                    if (tile.IsEmpty)
                    {
                        if (view != null) Destroy(view.gameObject);
                        _views[x, y] = null;
                        continue;
                    }

                    if (view == null)
                    {
                        view = NewView(tile, new Vec2I(x, y));
                        _views[x, y] = view;
                    }
                    view.SetTile(tile);
                    view.Cell = new Vec2I(x, y);
                    view.Scale = 1f;
                    view.transform.localPosition = CellToLocal(x, y);
                }
            }
        }

        // ---------- playback ----------

        public IEnumerator PlaySwap(Vec2I a, Vec2I b)
        {
            TileView va = _views[a.X, a.Y];
            TileView vb = _views[b.X, b.Y];
            _views[a.X, a.Y] = vb;
            _views[b.X, b.Y] = va;
            if (va != null) va.Cell = b;
            if (vb != null) vb.Cell = a;

            var routines = new List<IEnumerator>();
            if (va != null) routines.Add(MoveTo(va, CellToLocal(b.X, b.Y), 0.16f, false));
            if (vb != null) routines.Add(MoveTo(vb, CellToLocal(a.X, a.Y), 0.16f, false));
            yield return Parallel(routines);
        }

        public IEnumerator PlayResult(TurnResult result, Board board)
        {
            for (int i = 0; i < result.Steps.Count; i++)
            {
                CascadeStep step = result.Steps[i];
                var routines = new List<IEnumerator>();

                foreach (SpecialActivation activation in step.Activations)
                    routines.Add(PlayActivation(activation));

                foreach (ClearedTile cleared in step.Cleared)
                {
                    TileView view = _views[cleared.Position.X, cleared.Position.Y];
                    if (view == null) continue;
                    _views[cleared.Position.X, cleared.Position.Y] = null;
                    routines.Add(Pop(view));
                }

                foreach (CreatedTile created in step.Created)
                {
                    TileView view = _views[created.Position.X, created.Position.Y];
                    if (view == null) continue;
                    view.SetTile(created.Tile);
                    routines.Add(Punch(view));
                }

                if (step.Activations.Count > 0) routines.Add(Shake(0.18f, 0.08f + 0.02f * Mathf.Min(step.Activations.Count, 5)));

                yield return Parallel(routines);
                routines.Clear();

                foreach (TileMove move in step.Moves)
                {
                    TileView view = _views[move.From.X, move.From.Y];
                    if (view == null) continue;
                    _views[move.From.X, move.From.Y] = null;
                    _views[move.To.X, move.To.Y] = view;
                    view.Cell = move.To;
                    routines.Add(Fall(view, move.To.Y));
                }

                foreach (TileDrop drop in step.Drops)
                {
                    TileView view = NewView(drop.Tile, drop.To);
                    view.transform.localPosition = CellToLocal(drop.To.X, drop.FromY);
                    _views[drop.To.X, drop.To.Y] = view;
                    routines.Add(Fall(view, drop.To.Y));
                }

                yield return Parallel(routines);
                yield return new WaitForSeconds(0.04f);
            }
        }

        public IEnumerator PlayReshuffle(Board board)
        {
            yield return Shake(0.3f, 0.15f);
            Resync(board);
            var routines = new List<IEnumerator>();
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    if (_views[x, y] != null) routines.Add(Punch(_views[x, y]));
            yield return Parallel(routines);
        }

        // ---------- hint ----------

        public void ShowHint(Vec2I a, Vec2I b)
        {
            StopHint();
            _hinted.Clear();
            if (_views[a.X, a.Y] != null) _hinted.Add(_views[a.X, a.Y]);
            if (_views[b.X, b.Y] != null) _hinted.Add(_views[b.X, b.Y]);
            _hint = StartCoroutine(HintPulse());
        }

        public void StopHint()
        {
            if (_hint != null) StopCoroutine(_hint);
            _hint = null;
            foreach (TileView view in _hinted)
                if (view != null) view.Scale = 1f;
            _hinted.Clear();
        }

        private IEnumerator HintPulse()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * 6f;
                float scale = 1f + 0.12f * Mathf.Sin(t);
                foreach (TileView view in _hinted)
                    if (view != null) view.Scale = scale;
                yield return null;
            }
        }

        // ---------- animation pieces ----------

        private IEnumerator Parallel(List<IEnumerator> routines)
        {
            int pending = routines.Count;
            foreach (IEnumerator routine in routines)
                StartCoroutine(Track(routine, () => pending--));
            while (pending > 0) yield return null;
        }

        private IEnumerator Track(IEnumerator routine, Action done)
        {
            yield return StartCoroutine(routine);
            done();
        }

        private static IEnumerator Tween(float duration, Action<float> onProgress)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                onProgress(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            onProgress(1f);
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private IEnumerator MoveTo(TileView view, Vector3 target, float duration, bool bounce)
        {
            Vector3 start = view.transform.localPosition;
            yield return Tween(duration, t =>
            {
                if (view == null) return;
                float e = bounce ? EaseOutBack(t) : EaseOutQuad(t);
                view.transform.localPosition = Vector3.LerpUnclamped(start, target, e);
            });
        }

        private IEnumerator Fall(TileView view, int targetY)
        {
            Vector3 target = CellToLocal(view.Cell.X, targetY);
            float distance = Mathf.Abs(view.transform.localPosition.y - target.y);
            yield return MoveTo(view, target, 0.12f + 0.05f * distance, true);
        }

        private IEnumerator Punch(TileView view)
        {
            yield return Tween(0.22f, t =>
            {
                if (view == null) return;
                view.Scale = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);
            });
            if (view != null) view.Scale = 1f;
        }

        private IEnumerator Pop(TileView view)
        {
            Color color = view.Tile.Special == SpecialType.ColorBomb
                ? Color.white
                : SpriteFactory.Palette[view.Tile.Color % SpriteFactory.Palette.Length];
            Vector3 position = view.transform.localPosition;
            SpawnBurst(position, color);
            view.SetSortingBoost(5);

            yield return Tween(0.2f, t =>
            {
                if (view == null) return;
                view.Scale = t < 0.3f ? 1f + 0.25f * (t / 0.3f) : 1.25f * (1f - (t - 0.3f) / 0.7f);
            });
            if (view != null) Destroy(view.gameObject);
        }

        private void SpawnBurst(Vector3 localPosition, Color color)
        {
            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject("Spark");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = localPosition;
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = SpriteFactory.Circle();
                r.color = color;
                r.sortingOrder = 18;
                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                float speed = 0.8f + UnityEngine.Random.value * 0.9f;
                StartCoroutine(Spark(go, r, direction * speed));
            }
        }

        private IEnumerator Spark(GameObject go, SpriteRenderer renderer, Vector3 velocity)
        {
            Vector3 start = go.transform.localPosition;
            Color color = renderer.color;
            yield return Tween(0.35f, t =>
            {
                if (go == null) return;
                go.transform.localPosition = start + velocity * t;
                go.transform.localScale = Vector3.one * 0.18f * (1f - t);
                color.a = 1f - t;
                renderer.color = color;
            });
            if (go != null) Destroy(go);
        }

        private IEnumerator PlayActivation(SpecialActivation activation)
        {
            var go = new GameObject("Blast");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = 20;
            Vector3 center = CellToLocal(activation.Origin.X, activation.Origin.Y);

            switch (activation.Type)
            {
                case SpecialType.RowRocket:
                    r.sprite = SpriteFactory.Square();
                    go.transform.localPosition = new Vector3((_width - 1) / 2f, center.y, 0);
                    break;
                case SpecialType.ColumnRocket:
                    r.sprite = SpriteFactory.Square();
                    go.transform.localPosition = new Vector3(center.x, (_height - 1) / 2f, 0);
                    break;
                case SpecialType.ColorBomb:
                    r.sprite = SpriteFactory.Square();
                    go.transform.localPosition = new Vector3((_width - 1) / 2f, (_height - 1) / 2f, 0);
                    break;
                default:
                    r.sprite = SpriteFactory.Circle();
                    go.transform.localPosition = center;
                    break;
            }

            SpecialType type = activation.Type;
            yield return Tween(0.3f, t =>
            {
                if (go == null) return;
                float fade = 1f - t;
                switch (type)
                {
                    case SpecialType.RowRocket:
                        go.transform.localScale = new Vector3(_width + 1f, 0.5f * fade + 0.1f, 1f);
                        break;
                    case SpecialType.ColumnRocket:
                        go.transform.localScale = new Vector3(0.5f * fade + 0.1f, _height + 1f, 1f);
                        break;
                    case SpecialType.ColorBomb:
                        go.transform.localScale = new Vector3(_width + 1f, _height + 1f, 1f);
                        break;
                    default:
                        go.transform.localScale = Vector3.one * (1f + 2.6f * EaseOutQuad(t));
                        break;
                }
                r.color = new Color(1f, 1f, 1f, 0.7f * fade * (type == SpecialType.ColorBomb ? 0.6f : 1f));
            });
            if (go != null) Destroy(go);
        }

        private IEnumerator Shake(float duration, float strength)
        {
            yield return Tween(duration, t =>
            {
                float falloff = 1f - t;
                transform.position = new Vector3(
                    (UnityEngine.Random.value - 0.5f) * 2f * strength * falloff,
                    (UnityEngine.Random.value - 0.5f) * 2f * strength * falloff,
                    0f);
            });
            transform.position = Vector3.zero;
        }
    }
}
