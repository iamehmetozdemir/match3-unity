using System;
using System.Collections;
using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Glue between input, the game session and the board view, plus the HUD.
    /// It creates itself when the game starts, so no scene setup is needed:
    /// press Play in any scene (even an empty one) and the game runs.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private const float SwipeThreshold = 0.4f;   // in cells
        private const float HintDelay = 6f;          // seconds of inactivity before a hint

        private static GameController _instance;

        public static GameController Instance => _instance;
        public GameSession Session => _session;
        public BoardView BoardView => _boardView;
        /// <summary>True while a turn is being animated.</summary>
        public bool IsBusy => _busy;

        /// <summary>Plays a swap exactly as if the player had made it. Returns false if input is currently blocked.</summary>
        public bool TryPlay(Vec2I a, Vec2I b)
        {
            if (_busy || _session == null || _session.State != GameState.Playing) return false;
            RequestMove(a, b);
            return true;
        }

        private Camera _camera;
        private BoardView _boardView;
        private GameSession _session;
        private int _levelIndex;
        private bool _busy;
        private float _idleTimer;
        private bool _hintShown;

        private bool _dragging;
        private bool _swipeDone;
        private Vec2I _dragCell;
        private Vector3 _dragWorld;
        private Vec2I? _selected;

        // HUD
        private GUIStyle _label;
        private GUIStyle _big;
        private GUIStyle _button;
        private int _styleUnit;

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
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.06f, 0.07f, 0.12f);
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _camera.transform.rotation = Quaternion.identity;

            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(transform, false);
            _boardView = boardObject.AddComponent<BoardView>();

            StartLevel(0);
        }

        private void StartLevel(int index)
        {
            StopAllCoroutines();
            _levelIndex = index;
            _session = new GameSession(Levels.All[index], new SystemRandom(Environment.TickCount));
            _boardView.Build(_session.Board);
            _busy = false;
            _selected = null;
            _dragging = false;
            _idleTimer = 0f;
            _hintShown = false;
            FitCamera();
        }

        // ---------- per frame ----------

        private void Update()
        {
            if (_session == null) return;
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

        private void FitCamera()
        {
            // Leave 2 units above the board for the HUD and half a unit below it.
            float neededHeight = _boardView.Height + 2.5f;
            float neededWidth = _boardView.Width + 1f;
            float size = Mathf.Max(neededHeight / 2f, neededWidth / (2f * _camera.aspect));
            _camera.orthographicSize = size;
            _camera.transform.position = new Vector3((_boardView.Width - 1) / 2f, (_boardView.Height - 1) / 2f + 0.75f, -10f);
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

                if (_boardView.TryGetCell(world, out Vec2I cell))
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

            if (_dragging && !_swipeDone && PointerInput.IsHeld)
            {
                Vector3 delta = world - _dragWorld;
                if (delta.magnitude > SwipeThreshold)
                {
                    Vec2I target = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                        ? new Vec2I(_dragCell.X + (delta.x > 0 ? 1 : -1), _dragCell.Y)
                        : new Vec2I(_dragCell.X, _dragCell.Y + (delta.y > 0 ? 1 : -1));
                    _swipeDone = true;
                    _dragging = false;
                    ClearSelection();
                    if (_session.Board.InBounds(target)) RequestMove(_dragCell, target);
                }
            }

            if (PointerInput.WasReleasedThisFrame)
            {
                if (_dragging && !_swipeDone) OnTap(_dragCell);
                _dragging = false;
            }
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
            if (!_busy) StartCoroutine(MoveRoutine(a, b));
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
                yield return _boardView.PlaySwap(a, b); // swap back
            }
            else
            {
                yield return _boardView.PlayResult(result, _session.Board);
                if (result.Reshuffled) yield return _boardView.PlayReshuffle(_session.Board);
            }

            _boardView.Resync(_session.Board);
            _busy = false;
        }

        // ---------- HUD ----------

        // Base size for text and spacing: follows the smaller screen side, so it fits phones in portrait too.
        private static float Unit => Mathf.Max(12f, Mathf.Min(Screen.width / 14f, Screen.height / 28f));

        private void EnsureStyles()
        {
            int unit = Mathf.RoundToInt(Unit);
            if (_label != null && _styleUnit == unit) return;
            _styleUnit = unit;

            _label = new GUIStyle(GUI.skin.label) { fontSize = unit, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            _label.normal.textColor = Color.white;
            _big = new GUIStyle(_label) { fontSize = unit * 2 };
            _button = new GUIStyle(GUI.skin.button) { fontSize = unit, fontStyle = FontStyle.Bold };
        }

        private void OnGUI()
        {
            if (_session == null) return;
            EnsureStyles();

            float unit = Unit;
            float width = Screen.width;

            // The HUD sits right above the board: level, moves and score in one row, a progress bar below.
            float boardTop = Screen.height - _camera.WorldToScreenPoint(new Vector3(0f, _boardView.Height - 0.5f + 0.25f, 0f)).y;
            float rowY = Mathf.Max(4f, boardTop - unit * 4.4f);
            float rowHeight = unit * 1.6f;

            GUI.Label(new Rect(0, rowY, width * 0.34f, rowHeight), _session.Level.Name, _label);
            GUI.Label(new Rect(width * 0.33f, rowY, width * 0.34f, rowHeight), "Moves " + _session.MovesLeft, _label);
            GUI.Label(new Rect(width * 0.66f, rowY, width * 0.34f, rowHeight),
                _session.Score + " / " + _session.Level.TargetScore, _label);

            float barWidth = width * 0.86f;
            var bar = new Rect((width - barWidth) / 2f, rowY + rowHeight + unit * 0.3f, barWidth, unit * 0.55f);
            Color previous = GUI.color;
            GUI.color = new Color(0.15f, 0.17f, 0.27f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            float progress = Mathf.Clamp01((float)_session.Score / _session.Level.TargetScore);
            GUI.color = new Color(0.98f, 0.80f, 0.20f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * progress, bar.height), Texture2D.whiteTexture);
            GUI.color = previous;

            if (_busy || _session.State == GameState.Playing) return;
            DrawEndOverlay();
        }

        private void DrawEndOverlay()
        {
            float unit = Unit;
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            bool won = _session.State == GameState.Won;
            bool lastLevel = _levelIndex >= Levels.All.Count - 1;

            float top = Screen.height * 0.5f - unit * 5f;
            string title = won ? (lastLevel ? "You finished the game!" : "Level complete!") : "Out of moves";
            GUI.Label(new Rect(0, top, Screen.width, unit * 3f), title, _big);
            GUI.Label(new Rect(0, top + unit * 3f, Screen.width, unit * 1.6f), "Score " + _session.Score, _label);

            float buttonWidth = Mathf.Min(Screen.width * 0.7f, unit * 12f);
            var buttonRect = new Rect((Screen.width - buttonWidth) / 2f, top + unit * 5.5f, buttonWidth, unit * 2.4f);

            if (won)
            {
                string text = lastLevel ? "Play again" : "Next level";
                if (GUI.Button(buttonRect, text, _button)) StartLevel(lastLevel ? 0 : _levelIndex + 1);
            }
            else
            {
                if (GUI.Button(buttonRect, "Try again", _button)) StartLevel(_levelIndex);
            }
        }
    }
}
