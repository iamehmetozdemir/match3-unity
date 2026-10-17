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
        private SpriteRenderer[,] _ice;
        private SpriteRenderer[,] _boxes;
        private Font _font;
        private int _width;
        private int _height;
        private SpriteRenderer _selection;
        private Coroutine _hint;
        private readonly List<TileView> _hinted = new List<TileView>();

        public int Width => _width;
        public int Height => _height;

        /// <summary>Optional; set by the controller.</summary>
        public SoundFx Sound { get; set; }

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
            _ice = new SpriteRenderer[_width, _height];
            _boxes = new SpriteRenderer[_width, _height];

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

            SyncObstacles(board);
        }

        /// <summary>Creates, updates or removes the ice and box sprites so they match the board.</summary>
        private void SyncObstacles(Board board)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    SyncObstacle(ref _ice[x, y], board.IceAt(x, y), x, y, "Ice", 1, SpriteFactory.Ice);
                    SyncObstacle(ref _boxes[x, y], board.BoxAt(x, y), x, y, "Box", 9, SpriteFactory.Box);
                }
            }
        }

        private void SyncObstacle(ref SpriteRenderer renderer, int amount, int x, int y, string name, int order, Func<int, Sprite> sprite)
        {
            if (amount <= 0)
            {
                if (renderer != null) Destroy(renderer.gameObject);
                renderer = null;
                return;
            }

            if (renderer == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = CellToLocal(x, y);
                renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = order;
            }
            renderer.sprite = sprite(amount);
            renderer.color = Color.white;
            renderer.transform.localScale = Vector3.one;
            renderer.transform.localRotation = Quaternion.identity;
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
            // The square sprite's shape fills 78% of the sprite, so scale up to cover the board plus a margin.
            frame.transform.localScale = new Vector3((_width + 0.3f) / 0.78f, (_height + 0.3f) / 0.78f, 1f);

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
            SyncObstacles(board);
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

            Sound?.Play(Sfx.Swap);
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

                PlayStepSounds(step);

                foreach (SpecialActivation activation in step.Activations)
                    routines.Add(PlayActivation(activation));

                foreach (ObstacleHit hit in step.IceCracked)
                    routines.Add(CrackIce(hit));

                foreach (ObstacleHit hit in step.BoxesHit)
                    routines.Add(HitBox(hit));

                if (step.Score > 0) SpawnScorePopup(step);

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
                    _views[drop.To.X, drop.To.Y] = view;
                    if (drop.FromY > drop.To.Y)
                    {
                        view.transform.localPosition = CellToLocal(drop.To.X, drop.FromY);
                        routines.Add(Fall(view, drop.To.Y));
                    }
                    else
                    {
                        // A cell under a box: the new tile grows in place.
                        view.transform.localPosition = CellToLocal(drop.To.X, drop.To.Y);
                        view.Scale = 0f;
                        routines.Add(Grow(view));
                    }
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

        private void PlayStepSounds(CascadeStep step)
        {
            if (Sound == null) return;
            if (step.Cleared.Count > 0) Sound.Play(Sfx.Match, Mathf.Min(1f + 0.09f * (step.Level - 1), 1.7f));
            if (step.Created.Count > 0) Sound.Play(Sfx.Special);
            if (step.IceCracked.Count > 0) Sound.Play(Sfx.Ice, 1f, 0.8f);
            if (step.BoxesHit.Count > 0) Sound.Play(Sfx.Box);

            bool rocket = false, bomb = false, colorBomb = false;
            foreach (SpecialActivation a in step.Activations)
            {
                rocket |= a.Type == SpecialType.RowRocket || a.Type == SpecialType.ColumnRocket;
                bomb |= a.Type == SpecialType.Bomb;
                colorBomb |= a.Type == SpecialType.ColorBomb;
            }
            if (rocket) Sound.Play(Sfx.Rocket);
            if (bomb) Sound.Play(Sfx.Bomb);
            if (colorBomb) Sound.Play(Sfx.ColorBomb);
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

        private IEnumerator Grow(TileView view)
        {
            yield return Tween(0.22f, t =>
            {
                if (view != null) view.Scale = EaseOutBack(t);
            });
            if (view != null) view.Scale = 1f;
        }

        private IEnumerator CrackIce(ObstacleHit hit)
        {
            SpriteRenderer ice = _ice[hit.Position.X, hit.Position.Y];
            if (ice == null) yield break;
            SpawnBurst(ice.transform.localPosition, new Color(0.85f, 0.95f, 1f));

            if (hit.Remaining > 0)
            {
                ice.sprite = SpriteFactory.Ice(hit.Remaining);
                yield return Tween(0.15f, t => { if (ice != null) ice.transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(t * Mathf.PI)); });
                yield break;
            }

            _ice[hit.Position.X, hit.Position.Y] = null;
            yield return Tween(0.25f, t =>
            {
                if (ice == null) return;
                ice.color = new Color(1f, 1f, 1f, 1f - t);
                ice.transform.localScale = Vector3.one * (1f + 0.2f * t);
            });
            if (ice != null) Destroy(ice.gameObject);
        }

        private IEnumerator HitBox(ObstacleHit hit)
        {
            SpriteRenderer box = _boxes[hit.Position.X, hit.Position.Y];
            if (box == null) yield break;
            SpawnBurst(box.transform.localPosition, new Color(0.66f, 0.43f, 0.22f));

            if (hit.Remaining > 0)
            {
                box.sprite = SpriteFactory.Box(hit.Remaining);
                Vector3 home = box.transform.localPosition;
                yield return Tween(0.2f, t =>
                {
                    if (box == null) return;
                    box.transform.localPosition = home + new Vector3(Mathf.Sin(t * 40f) * 0.06f * (1f - t), 0f, 0f);
                });
                if (box != null) box.transform.localPosition = home;
                yield break;
            }

            _boxes[hit.Position.X, hit.Position.Y] = null;
            yield return Tween(0.22f, t =>
            {
                if (box == null) return;
                box.transform.localScale = Vector3.one * (1f + 0.2f * t) * (1f - t);
                box.transform.localRotation = Quaternion.Euler(0f, 0f, 25f * t);
            });
            if (box != null) Destroy(box.gameObject);
        }

        private void SpawnScorePopup(CascadeStep step)
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) return;

            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (ClearedTile c in step.Cleared) { center += CellToLocal(c.Position.X, c.Position.Y); count++; }
            if (count == 0) return;
            center /= count;

            var go = new GameObject("Score");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center + new Vector3(0f, 0f, -0.5f);
            var text = go.AddComponent<TextMesh>();
            text.font = _font;
            text.text = "+" + step.Score;
            text.fontSize = 64;
            text.characterSize = 0.05f + 0.006f * Mathf.Min(step.Level, 6);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            Color color = step.Level > 1 ? new Color(1f, 0.82f, 0.3f) : Color.white;
            text.color = color;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _font.material;
            renderer.sortingOrder = 40;

            StartCoroutine(FloatAway(go, text, color));
        }

        private IEnumerator FloatAway(GameObject go, TextMesh text, Color color)
        {
            Vector3 start = go.transform.localPosition;
            yield return Tween(0.8f, t =>
            {
                if (go == null) return;
                go.transform.localPosition = start + new Vector3(0f, 0.9f * EaseOutQuad(t), 0f);
                color.a = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
                text.color = color;
            });
            if (go != null) Destroy(go);
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
