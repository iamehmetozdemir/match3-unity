using System;
using System.Collections;
using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Glue between input, the game session, the board view, sound and the interface.
    /// It creates itself when the game starts, so no scene setup is needed:
    /// press Play in any scene (even an empty one) and the game runs.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private const float SwipeThreshold = 0.4f;   // in cells
        private const float HintDelay = 6f;          // seconds of inactivity before a hint
        private const float BannerTime = 1.8f;       // how long the level intro stays up

        private enum Screen { Menu, Game }

        private static GameController _instance;

        public static GameController Instance => _instance;
        public GameSession Session => _session;
        public BoardView BoardView => _boardView;
        /// <summary>True while a turn is being animated.</summary>
        public bool IsBusy => _busy;
        public bool InMenu => _screen == Screen.Menu;

        private Camera _camera;
        private BoardView _boardView;
        private SoundFx _sound;
        private GameSession _session;
        private Screen _screen = Screen.Menu;
        private int _levelIndex;
        private bool _busy;
        private bool _endHandled;
        private float _idleTimer;
        private bool _hintShown;
        private float _levelStartTime;
        private bool _hammerArmed;

        private bool _dragging;
        private bool _swipeDone;
        private Vec2I _dragCell;
        private Vector3 _dragWorld;
        private Vec2I? _selected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            new GameObject("Match3").AddComponent<GameController>();
        }

        private void Awake()
        {
            _instance = this;
            Application.targetFrameRate = 60;

            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = cameraObject.AddComponent<Camera>();
            }
            if (_camera.GetComponent<AudioListener>() == null && FindAnyObjectByType<AudioListener>() == null)
                _camera.gameObject.AddComponent<AudioListener>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Hud.Background;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _camera.transform.rotation = Quaternion.identity;

            _sound = gameObject.AddComponent<SoundFx>();

            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(transform, false);
            _boardView = boardObject.AddComponent<BoardView>();
            _boardView.Sound = _sound;

            ShowMenu();
        }

        // ---------- screens ----------

        public void ShowMenu()
        {
            StopAllCoroutines();
            _boardView.StopHint();
            _boardView.gameObject.SetActive(false);
            _session = null;
            _busy = false;
            _screen = Screen.Menu;
        }

        public void StartLevel(int index)
        {
            StopAllCoroutines();
            _levelIndex = Mathf.Clamp(index, 0, Levels.All.Count - 1);
            _session = new GameSession(Levels.All[_levelIndex], new SystemRandom(Environment.TickCount));
            _boardView.gameObject.SetActive(true);
            _boardView.Build(_session.Board);
            _screen = Screen.Game;
            _busy = false;
            _endHandled = false;
            _selected = null;
            _dragging = false;
            _idleTimer = 0f;
            _hintShown = false;
            _hammerArmed = false;
            _levelStartTime = Time.time;
            FitCamera();
        }

        /// <summary>Uses the shuffle booster, as if the player did it. False if it can't be used now.</summary>
        public bool TryShuffle()
        {
            if (_busy || _session == null || _session.State != GameState.Playing || _session.Shuffles <= 0) return false;
            StartCoroutine(ShuffleRoutine());
            return true;
        }

        /// <summary>Uses the hammer booster on a cell, as if the player did it. False if it can't be used now.</summary>
        public bool TryHammer(Vec2I cell)
        {
            if (_busy || _session == null || _session.State != GameState.Playing || _session.Hammers <= 0) return false;
            StartCoroutine(HammerRoutine(cell));
            return true;
        }

        /// <summary>Plays a swap exactly as if the player had made it. Returns false if input is currently blocked.</summary>
        public bool TryPlay(Vec2I a, Vec2I b)
        {
            if (_busy || _session == null || _session.State != GameState.Playing) return false;
            RequestMove(a, b);
            return true;
        }

        // ---------- per frame ----------

        private void Update()
        {
            if (_screen != Screen.Game || _session == null) return;
            FitCamera();

            if (_busy || _session.State != GameState.Playing) return;

            _idleTimer += Time.deltaTime;
            if (!_hintShown && _idleTimer > HintDelay)
            {
                if (_session.Board.TryFindValidMove(out Vec2I a, out Vec2I b)) _boardView.ShowHint(a, b);
                _hintShown = true;
            }

            HandleInput();
        }

        // Puts the board in the space left under the HUD, as large as fits.
        private void FitCamera()
        {
            float unit = Hud.Unit;
            float hudPixels = unit * 7f;
            float bottomPixels = unit * 3.8f; // room for the booster buttons
            float available = Mathf.Max(1f, UnityEngine.Screen.height - hudPixels - bottomPixels);

            float pixelsPerUnit = Mathf.Min(available / (_boardView.Height + 0.5f), UnityEngine.Screen.width / (_boardView.Width + 0.6f));
            _camera.orthographicSize = UnityEngine.Screen.height / (2f * pixelsPerUnit);

            float boardCenterScreen = bottomPixels + available / 2f;
            float offset = (UnityEngine.Screen.height / 2f - boardCenterScreen) / pixelsPerUnit;
            _camera.transform.position = new Vector3((_boardView.Width - 1) / 2f, (_boardView.Height - 1) / 2f + offset, -10f);
        }

        private void HandleInput()
        {
            Vector2 screen = PointerInput.Position;
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));

            if (PointerInput.WasPressedThisFrame)
            {
                _hintShown = false;
                _idleTimer = 0f;
                _boardView.StopHint();

                if (_hammerArmed)
                {
                    if (_boardView.TryGetCell(world, out Vec2I target)) TryHammer(target);
                    _dragging = false;
                    return;
                }

                if (_boardView.TryGetCell(world, out Vec2I cell) && !_session.Board.HasBox(cell))
                {
                    _dragging = true;
                    _swipeDone = false;
                    _dragCell = cell;
                    _dragWorld = world;
                }
                else
                {
                    _dragging = false;
                }
            }

            // A swipe is detected while the pointer is held, or on release for very quick flicks.
            bool released = PointerInput.WasReleasedThisFrame;
            if (_dragging && !_swipeDone && (PointerInput.IsHeld || released) && TrySwipe(world)) return;

            if (released)
            {
                if (_dragging && !_swipeDone) OnTap(_dragCell);
                _dragging = false;
            }
        }

        private bool TrySwipe(Vector3 world)
        {
            Vector3 delta = world - _dragWorld;
            if (delta.magnitude <= SwipeThreshold) return false;

            Vec2I target = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? new Vec2I(_dragCell.X + (delta.x > 0 ? 1 : -1), _dragCell.Y)
                : new Vec2I(_dragCell.X, _dragCell.Y + (delta.y > 0 ? 1 : -1));
            _swipeDone = true;
            _dragging = false;
            ClearSelection();
            if (_session.Board.InBounds(target)) RequestMove(_dragCell, target);
            return true;
        }

        private void OnTap(Vec2I cell)
        {
            if (_selected.HasValue)
            {
                Vec2I first = _selected.Value;
                if (first == cell)
                {
                    ClearSelection();
                }
                else if (first.IsAdjacentTo(cell))
                {
                    ClearSelection();
                    RequestMove(first, cell);
                }
                else
                {
                    _selected = cell;
                    _boardView.ShowSelection(cell);
                }
                return;
            }

            _selected = cell;
            _boardView.ShowSelection(cell);
        }

        private void ClearSelection()
        {
            _selected = null;
            _boardView.ShowSelection(null);
        }

        private void RequestMove(Vec2I a, Vec2I b)
        {
            if (_busy) return;
            if (_session.Board.HasBox(a) || _session.Board.HasBox(b))
            {
                _sound.Play(Sfx.Invalid);
                return;
            }
            StartCoroutine(MoveRoutine(a, b));
        }

        private IEnumerator MoveRoutine(Vec2I a, Vec2I b)
        {
            _busy = true;
            _idleTimer = 0f;
            _hintShown = false;
            _boardView.StopHint();

            // The core resolves the whole turn immediately; the view then replays it.
            TurnResult result = _session.TryMove(a, b);

            yield return _boardView.PlaySwap(a, b);
            if (!result.Valid)
            {
                _sound.Play(Sfx.Invalid);
                yield return _boardView.PlaySwap(a, b); // swap back
            }
            else
            {
                yield return _boardView.PlayResult(result, _session.Board);
                if (result.Reshuffled) yield return _boardView.PlayReshuffle(_session.Board);
            }

            _boardView.Resync(_session.Board);
            HandleLevelEnd();
            _busy = false;
        }

        private IEnumerator HammerRoutine(Vec2I cell)
        {
            _busy = true;
            _hammerArmed = false;
            _boardView.StopHint();
            ClearSelection();

            TurnResult result = _session.UseHammer(cell);
            if (result.Valid)
            {
                yield return _boardView.PlayHammer(cell);
                yield return _boardView.PlayResult(result, _session.Board);
                if (result.Reshuffled) yield return _boardView.PlayReshuffle(_session.Board);
            }

            _boardView.Resync(_session.Board);
            HandleLevelEnd();
            _busy = false;
        }

        private IEnumerator ShuffleRoutine()
        {
            _busy = true;
            _hammerArmed = false;
            _boardView.StopHint();
            ClearSelection();

            if (_session.UseShuffle().Valid)
            {
                _sound.Play(Sfx.Special);
                yield return _boardView.PlayReshuffle(_session.Board);
            }

            _boardView.Resync(_session.Board);
            _busy = false;
        }

        private void HandleLevelEnd()
        {
            if (_endHandled || _session.State == GameState.Playing) return;
            _endHandled = true;
            if (_session.State == GameState.Won)
            {
                Progress.Record(_levelIndex, _session.Stars);
                _sound.Play(Sfx.Win);
            }
            else
            {
                _sound.Play(Sfx.Lose);
            }
        }

        // ---------- interface ----------

        private void OnGUI()
        {
            Hud.EnsureStyles();
            if (_screen == Screen.Menu) DrawMenu();
            else if (_session != null) DrawGame();
        }

        private bool Button(Rect rect, string text, bool primary = true)
        {
            if (!GUI.Button(rect, text, primary ? Hud.Button : Hud.SecondaryButton)) return false;
            _sound.Play(Sfx.Click);
            return true;
        }

        private void SoundToggle(Rect rect)
        {
            if (Button(rect, _sound.Muted ? "Muted" : "Sound", false)) _sound.Muted = !_sound.Muted;
        }

        private void DrawMenu()
        {
            float u = Hud.Unit;
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            float pad = u * 0.8f;

            SoundToggle(new Rect(w - pad - u * 4.4f, pad, u * 4.4f, u * 1.8f));

            float y = Mathf.Max(u * 3f, h * 0.07f);
            GUI.Label(new Rect(0, y, w, u * 3f), "Match-3", Hud.Title);
            y += u * 3f;
            GUI.Label(new Rect(pad, y, w - pad * 2f, u * 1.4f), "Swap tiles, make matches and clear the goals.", Hud.Small);
            y += u * 1.6f;

            // Total stars
            float starSize = u * 1.4f;
            string total = Progress.TotalStars + " / " + Levels.All.Count * 3;
            float totalW = Hud.Text.CalcSize(new GUIContent(total)).x + u; // a little slack so it never wraps
            float totalX = (w - starSize - totalW) / 2f;
            Hud.Icon(new Rect(totalX, y, starSize, starSize), SpriteFactory.Star(true));
            GUI.Label(new Rect(totalX + starSize, y, totalW, starSize), total, Hud.Text);
            y += starSize + u * 1.2f;

            // Level cards
            int count = Levels.All.Count;
            int columns = w > h * 0.9f ? 4 : 2;
            int rows = Mathf.CeilToInt(count / (float)columns);
            float gap = u * 0.6f;
            float footer = u * 2.2f;
            float cardW = Mathf.Min((w - pad * 2f - gap * (columns - 1)) / columns, u * 8f);
            float cardH = Mathf.Min(cardW * 0.85f, (h - y - footer - gap * (rows - 1)) / rows);
            float gridW = cardW * columns + gap * (columns - 1);
            float gridX = (w - gridW) / 2f;

            for (int i = 0; i < count; i++)
            {
                int col = i % columns, row = i / columns;
                var card = new Rect(gridX + col * (cardW + gap), y + row * (cardH + gap), cardW, cardH);
                if (Button(card, "", false)) StartLevel(i);

                GUI.Label(new Rect(card.x, card.y + cardH * 0.06f, cardW, cardH * 0.36f), (i + 1).ToString(), Hud.Heading);

                Goal[] goals = Levels.All[i].Goals;
                float icon = Mathf.Min(cardH * 0.22f, u * 1.3f);
                float iconsW = goals.Length * icon + (goals.Length - 1) * icon * 0.2f;
                for (int g = 0; g < goals.Length; g++)
                    Hud.Icon(new Rect(card.x + (cardW - iconsW) / 2f + g * icon * 1.2f, card.y + cardH * 0.44f, icon, icon), Hud.GoalIcon(goals[g]));

                Hud.Stars(new Rect(card.x + cardW * 0.15f, card.y + cardH * 0.72f, cardW * 0.7f, cardH * 0.2f), Progress.Stars(i));
            }

            GUI.Label(new Rect(pad, h - footer, w - pad * 2f, footer), "Made with Unity  ·  github.com/iamehmetozdemir/match3-unity", Hud.Small);
        }

        private void DrawGame()
        {
            float u = Hud.Unit;
            float w = UnityEngine.Screen.width;
            float pad = u * 0.6f;

            // The HUD sits right above the board.
            float boardTop = UnityEngine.Screen.height - _camera.WorldToScreenPoint(new Vector3(0f, _boardView.Height - 0.5f + 0.25f, 0f)).y;
            float top = Mathf.Max(pad, boardTop - u * 6.8f);

            // Row 1: menu, level name, sound
            if (Button(new Rect(pad, top, u * 4.4f, u * 1.8f), "Menu", false)) { ShowMenu(); return; }
            GUI.Label(new Rect(0, top, w, u * 1.8f), _session.Level.Name, Hud.Text);
            SoundToggle(new Rect(w - pad - u * 4.4f, top, u * 4.4f, u * 1.8f));

            // Row 2: moves left and goals
            float rowY = top + u * 2.3f;
            float rowH = u * 3.4f;
            var moves = new Rect(pad, rowY, u * 4.4f, rowH);
            GUI.Box(moves, GUIContent.none, Hud.Panel);
            GUI.Label(new Rect(moves.x, moves.y + u * 0.2f, moves.width, u * 1.1f), "Moves", Hud.Small);
            GUI.Label(new Rect(moves.x, moves.y + u * 1.1f, moves.width, u * 2.1f), _session.MovesLeft.ToString(), Hud.Number);

            var goalsRect = new Rect(moves.xMax + u * 0.5f, rowY, w - moves.xMax - u * 0.5f - pad, rowH);
            GUI.Box(goalsRect, GUIContent.none, Hud.Panel);
            int n = _session.Goals.Count;
            float gap = u * 0.4f;
            float chipH = u * 2.3f;
            float chipW = Mathf.Min(u * 6.5f, (goalsRect.width - gap * (n + 1)) / n);
            float chipsW = chipW * n + gap * (n - 1);
            for (int i = 0; i < n; i++)
            {
                var chip = new Rect(goalsRect.x + (goalsRect.width - chipsW) / 2f + i * (chipW + gap), rowY + (rowH - chipH) / 2f, chipW, chipH);
                Hud.GoalChip(chip, _session.Goals[i]);
            }

            if (_session.State == GameState.Playing) DrawBoosters();
            if (Time.time - _levelStartTime < BannerTime && _session.State == GameState.Playing) DrawBanner();
            if (!_busy && _session.State != GameState.Playing) DrawEndPanel();
        }

        // Booster buttons under the board.
        private void DrawBoosters()
        {
            float u = Hud.Unit;
            float w = UnityEngine.Screen.width;
            float boardBottom = UnityEngine.Screen.height - _camera.WorldToScreenPoint(new Vector3(0f, -0.75f, 0f)).y;
            float height = u * 2.4f, width = Mathf.Min(u * 7.5f, w * 0.44f), gap = u * 0.6f;
            float y = boardBottom + u * 0.5f;

            var hammer = new Rect(w / 2f - gap / 2f - width, y, width, height);
            var shuffle = new Rect(w / 2f + gap / 2f, y, width, height);

            if (BoosterButton(hammer, SpriteFactory.Hammer(), _hammerArmed ? "Tap a tile" : "Hammer", _session.Hammers, _hammerArmed))
                _hammerArmed = !_hammerArmed;
            if (BoosterButton(shuffle, SpriteFactory.Shuffle(), "Shuffle", _session.Shuffles, false))
                StartCoroutine(ShuffleRoutine());
        }

        private bool BoosterButton(Rect rect, Sprite icon, string label, int count, bool active)
        {
            bool enabled = count > 0 && !_busy;
            GUI.enabled = enabled;
            Color previous = GUI.color;
            if (!enabled) GUI.color = new Color(1f, 1f, 1f, 0.45f);

            bool clicked = Button(rect, "", active);
            float pad = rect.height * 0.18f;
            float size = rect.height - pad * 2f;
            Hud.Icon(new Rect(rect.x + pad, rect.y + pad, size, size), icon);
            string text = active ? label : label + "  ×" + count;
            GUI.Label(new Rect(rect.x + size + pad * 1.5f, rect.y, rect.width - size - pad * 2.5f, rect.height), text, active ? Hud.BoosterTextDark : Hud.BoosterText);

            GUI.color = previous;
            GUI.enabled = true;
            return clicked;
        }

        private void DrawBanner()
        {
            float u = Hud.Unit;
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            float t = (Time.time - _levelStartTime) / BannerTime;
            float alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            var panel = new Rect((w - Mathf.Min(w * 0.9f, u * 16f)) / 2f, h * 0.42f - u * 3f, Mathf.Min(w * 0.9f, u * 16f), u * 6f);
            GUI.Box(panel, GUIContent.none, Hud.Panel);
            GUI.Label(new Rect(panel.x, panel.y + u * 0.4f, panel.width, u * 2f), _session.Level.Name, Hud.Heading);

            string text = "";
            foreach (GoalProgress goal in _session.Goals)
                text += (text.Length > 0 ? "  ·  " : "") + Hud.Describe(goal.Goal, goal.Target);
            GUI.Label(new Rect(panel.x + u, panel.y + u * 2.6f, panel.width - u * 2f, u * 2.8f), text, Hud.Text);

            GUI.color = previous;
        }

        private void DrawEndPanel()
        {
            float u = Hud.Unit;
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = previous;

            bool won = _session.State == GameState.Won;
            bool lastLevel = _levelIndex >= Levels.All.Count - 1;

            float panelW = Mathf.Min(w * 0.9f, u * 17f);
            float panelH = u * 14f;
            var panel = new Rect((w - panelW) / 2f, (h - panelH) / 2f, panelW, panelH);
            GUI.Box(panel, GUIContent.none, Hud.Panel);

            float y = panel.y + u;
            GUI.Label(new Rect(panel.x, y, panelW, u * 2.2f), won ? (lastLevel ? "You beat every level!" : "Level complete!") : "Out of moves", Hud.Heading);
            y += u * 2.6f;

            if (won)
            {
                Hud.Stars(new Rect(panel.x + panelW * 0.15f, y, panelW * 0.7f, u * 3.4f), _session.Stars);
                y += u * 3.8f;
            }
            else
            {
                GUI.Label(new Rect(panel.x + u, y, panelW - u * 2f, u * 3.4f), "So close! Try again with a new board.", Hud.Text);
                y += u * 3.8f;
            }
            GUI.Label(new Rect(panel.x, y, panelW, u * 1.6f), "Score " + _session.Score, Hud.Text);

            float buttonY = panel.yMax - u * 3.4f;
            float buttonW = (panelW - u * 3f) / 2f;
            var left = new Rect(panel.x + u, buttonY, buttonW, u * 2.4f);
            var right = new Rect(left.xMax + u, buttonY, buttonW, u * 2.4f);

            if (won && !lastLevel)
            {
                if (Button(left, "Next level")) StartLevel(_levelIndex + 1);
                if (Button(right, "Menu", false)) ShowMenu();
            }
            else if (won)
            {
                if (Button(left, "Menu")) ShowMenu();
                if (Button(right, "Replay", false)) StartLevel(_levelIndex);
            }
            else
            {
                if (Button(left, "Try again")) StartLevel(_levelIndex);
                if (Button(right, "Menu", false)) ShowMenu();
            }
        }
    }
}
