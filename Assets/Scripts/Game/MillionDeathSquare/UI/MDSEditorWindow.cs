using System.Text;
using Cysharp.Threading.Tasks;
using Seunghak.Manager;
using Seunghak.Platform;
using Seunghak.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 맵 에디터 (기획서 5장, 11장 화면 2): 왼쪽 도구, 가운데 칸(보드), 위쪽 시험 플레이/업로드.
    ///   - 마우스 왼쪽 클릭·드래그 = 배치, 오른쪽 = 지우기, 숫자 키 1~0 = 도구 1~10, Shift+숫자 = 도구 11~20
    ///   - Ctrl+Z 되돌리기, Ctrl+Y / Ctrl+Shift+Z 다시하기
    ///   - 장애물: 놓으면 "경로 그리기" — 클릭할 때마다 경로 지점 추가(최대 8, 반 칸 격자), Enter/오른쪽 클릭 = 완료
    ///   - "업로드"는 지금 맵을 직접 깨기 전까지 비활성 (어떤 편집이든 다시 잠김)
    /// </summary>
    public sealed class MDSEditorWindow : BaseUIWindow
    {
        public static readonly string[] ToolNames =
        {
            "벽", "바닥", "빈칸", "얼음", "컨베이어 위", "컨베이어 아래", "컨베이어 왼쪽", "컨베이어 오른쪽", "사라지는 바닥",
            "시작", "골인", "동전", "체크포인트", "열쇠(빨강)", "열쇠(초록)", "열쇠(파랑)", "문(빨강)", "문(초록)", "문(파랑)",
            "장애물", "지우개",
        };

        [Header("도구 (EditorTool 순서)")]
        [SerializeField] private Button[] toolButtons = new Button[21];
        [Header("위쪽")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button paletteButton;
        [SerializeField] private Button widthMinus, widthPlus, heightMinus, heightPlus;
        [SerializeField] private TMP_Text sizeText;
        [SerializeField] private Button undoButton, redoButton;
        [SerializeField] private Button newButton;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button pasteCodeButton;
        [SerializeField] private Button testPlayButton;
        [SerializeField] private Button uploadButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text statusText;
        [Header("장애물 패널")]
        [SerializeField] private GameObject obstaclePanel;
        [SerializeField] private TMP_Text obstacleText;
        [SerializeField] private Button modePingPong, modeLoop, modeCircle;
        [SerializeField] private Button[] speedButtons = new Button[3];
        [SerializeField] private Button delayMinus, delayPlus;
        [SerializeField] private Button radiusMinus, radiusPlus, angleMinus, anglePlus, directionButton;
        [SerializeField] private Button duplicateButton, deleteObstacleButton, finishPathButton;

        private MapEditorModel _model;
        private bool _pathDrawing;
        private bool _stroking;
        private bool _erasing;
        private int _lastCellX = -1, _lastCellY = -1;
        private float _previewTime;
        private bool _dirty;
        private float _saveTimer;
        private bool _uploading;
        private string _message = "";

        public MapEditorModel Model { get { return _model; } }

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.MDSEditorWindow;
            for (int i = 0; i < toolButtons.Length; i++)
            {
                EditorTool t = (EditorTool)i;
                MDSUi.Bind(toolButtons[i], () => SelectTool(t));
            }
            MDSUi.Bind(paletteButton, () => Do(_model.SetPalette((_model.Map.PaletteId + 1) % Rules.PALETTE_COUNT)));
            MDSUi.Bind(widthMinus, () => Do(_model.Resize(_model.Map.Width - 1, _model.Map.Height)));
            MDSUi.Bind(widthPlus, () => Do(_model.Resize(_model.Map.Width + 1, _model.Map.Height)));
            MDSUi.Bind(heightMinus, () => Do(_model.Resize(_model.Map.Width, _model.Map.Height - 1)));
            MDSUi.Bind(heightPlus, () => Do(_model.Resize(_model.Map.Width, _model.Map.Height + 1)));
            MDSUi.Bind(undoButton, () => { _model.Undo(); Rebuild(); });
            MDSUi.Bind(redoButton, () => { _model.Redo(); Rebuild(); });
            MDSUi.Bind(newButton, () => { _model.Load(MapData.CreateBlank(24, 14)); _pathDrawing = false; Rebuild(); });
            MDSUi.Bind(copyCodeButton, () => { MDSUi.CopyToClipboard(MapSerializer.ToShareCode(_model.Map)); _message = "공유 코드를 복사했어요"; RefreshUi(); });
            MDSUi.Bind(pasteCodeButton, PasteShareCode);
            MDSUi.Bind(testPlayButton, TestPlay);
            MDSUi.Bind(uploadButton, () => UploadAsync().Forget());
            MDSUi.Bind(backButton, ExitWindow);
            if (nameInput != null)
            {
                nameInput.characterLimit = Rules.MAX_NAME_LENGTH;
                nameInput.onEndEdit.AddListener(OnNameEdited);
            }

            MDSUi.Bind(modePingPong, () => Do(_model.SetPathMode(PathMode.PingPong)));
            MDSUi.Bind(modeLoop, () => Do(_model.SetPathMode(PathMode.Loop)));
            MDSUi.Bind(modeCircle, () => Do(_model.SetPathMode(PathMode.Circle)));
            for (int i = 0; i < speedButtons.Length; i++)
            {
                int tier = i + 1;
                MDSUi.Bind(speedButtons[i], () => Do(_model.SetSpeed(tier)));
            }
            MDSUi.Bind(delayMinus, () => Do(_model.SetDelay(SelectedDef().DelayTenths - DelayStep())));
            MDSUi.Bind(delayPlus, () => Do(_model.SetDelay(SelectedDef().DelayTenths + DelayStep())));
            MDSUi.Bind(radiusMinus, () => { ObstacleDef o = SelectedDef(); Do(_model.SetCircle(o.RadiusHalf - 1, o.StartAngle, o.Clockwise)); });
            MDSUi.Bind(radiusPlus, () => { ObstacleDef o = SelectedDef(); Do(_model.SetCircle(o.RadiusHalf + 1, o.StartAngle, o.Clockwise)); });
            MDSUi.Bind(angleMinus, () => { ObstacleDef o = SelectedDef(); Do(_model.SetCircle(o.RadiusHalf, (o.StartAngle + 15) & 15, o.Clockwise)); });
            MDSUi.Bind(anglePlus, () => { ObstacleDef o = SelectedDef(); Do(_model.SetCircle(o.RadiusHalf, (o.StartAngle + 1) & 15, o.Clockwise)); });
            MDSUi.Bind(directionButton, () => { ObstacleDef o = SelectedDef(); Do(_model.SetCircle(o.RadiusHalf, o.StartAngle, !o.Clockwise)); });
            MDSUi.Bind(duplicateButton, () => Do(_model.DuplicateSelected(5)));
            MDSUi.Bind(deleteObstacleButton, () => { Do(_model.DeleteObstacle(_model.SelectedObstacle)); _pathDrawing = false; });
            MDSUi.Bind(finishPathButton, () => { _pathDrawing = false; RefreshUi(); });

            for (int i = 0; i < toolButtons.Length && i < ToolNames.Length; i++)
            {
                string key = i < 9 ? (i + 1).ToString() : (i == 9 ? "0" : (i < 20 ? "S" + ((i - 9) % 10) : ""));
                MDSUi.SetLabel(toolButtons[i], ToolNames[i] + (key.Length > 0 ? "  " + key : ""));
            }
        }

        public override void EnterWindow()
        {
            base.EnterWindow();
            EditorDevice devices = MDSSaveStore.Campaign.UnlockedDevices;
            if (_model == null)
            {
                MapData draft = MDSSaveStore.LoadEditorDraft();
                _model = new MapEditorModel(draft ?? MapData.CreateBlank(24, 14), devices);
            }
            _model.Devices = devices;
            if (SoundManager.Instance != null) SoundManager.Instance.PlayBGM(SoundKeys.MDS_BGM_EDITOR);
            MDSAnalytics.Screen("editor");
            Rebuild();
        }

        public override void RestoreWindow()
        {
            base.RestoreWindow();
            if (_model != null) Rebuild();
        }

        public override void ExitWindow()
        {
            SaveDraft();
            MDSGameDirector d = MDSGameDirector.Instance;
            if (d != null && !d.IsPlaying && d.Board != null) d.Board.Clear();
            base.ExitWindow();
        }

        // ── 편집 ──

        private ObstacleDef SelectedDef()
        {
            int i = _model.SelectedObstacle;
            return i >= 0 && i < _model.Map.Obstacles.Count ? _model.Map.Obstacles[i] : new ObstacleDef();
        }

        private static int DelayStep() { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 10 : 1; }

        private void SelectTool(EditorTool t)
        {
            if (!_model.IsToolUnlocked(t))
            {
                _message = "캠페인 월드를 깨면 쓸 수 있어요";
                RefreshUi();
                return;
            }
            _model.CurrentTool = t;
            _pathDrawing = false;
            RefreshUi();
        }

        private void Do(EditResult r)
        {
            switch (r)
            {
                case EditResult.Locked: _message = "아직 잠긴 장치예요 (캠페인에서 해금)"; break;
                case EditResult.LimitReached: _message = "개수 제한에 걸렸어요 (장애물 60 · 동전 30 · 경로 지점 8)"; break;
                case EditResult.NotAllowedHere: _message = "여기에는 놓을 수 없어요"; break;
                case EditResult.Changed: _message = ""; break;
            }
            if (r == EditResult.Changed)
            {
                _dirty = true;
                if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(_model.CurrentTool == EditorTool.Eraser || _erasing ? SoundKeys.MDS_EDITOR_ERASE : SoundKeys.MDS_EDITOR_PLACE);
                Rebuild();
            }
            else RefreshUi();
        }

        private void OnNameEdited(string name)
        {
            string error;
            if (!_model.SetName(name, out error))
            {
                _message = error;
                if (nameInput != null) nameInput.SetTextWithoutNotify(_model.Map.Name);
            }
            else _dirty = true;
            RefreshUi();
        }

        private void PasteShareCode()
        {
            MapData map;
            string error;
            if (MapSerializer.TryParseShareCode(MDSUi.ReadClipboard(), out map, out error))
            {
                _model.Load(map);
                _pathDrawing = false;
                _message = "공유 코드에서 맵을 불러왔어요";
                _dirty = true;
                Rebuild();
            }
            else
            {
                _message = "클립보드에 올바른 공유 코드가 없어요 (" + error + ")";
                RefreshUi();
            }
        }

        private void Rebuild()
        {
            MDSGameDirector d = MDSGameDirector.Instance;
            if (d == null || d.IsPlaying) return;
            d.ShowEditorMap(_model.Map, _model.SelectedObstacle, PreviewTick);
            RefreshUi();
        }

        private int PreviewTick { get { return Mathf.FloorToInt(_previewTime * Fixed.TICKS_PER_SECOND); } }

        private void RefreshUi()
        {
            if (_model == null) return;
            MapData m = _model.Map;
            for (int i = 0; i < toolButtons.Length; i++)
            {
                EditorTool t = (EditorTool)i;
                bool unlocked = _model.IsToolUnlocked(t);
                MDSUi.Tint(toolButtons[i], t == _model.CurrentTool ? MDSUi.Selected : (unlocked ? MDSUi.Normal : MDSUi.Locked));
            }
            if (nameInput != null && !nameInput.isFocused) nameInput.SetTextWithoutNotify(m.Name);
            MDSUi.SetLabel(paletteButton, "팔레트: " + Palettes.Get(m.PaletteId).Name);
            MDSUi.SetText(sizeText, m.Width + " × " + m.Height);
            MDSUi.SetInteractable(undoButton, _model.CanUndo);
            MDSUi.SetInteractable(redoButton, _model.CanRedo);
            MDSUi.SetInteractable(uploadButton, _model.CanUpload && !_uploading);

            bool hasSel = _model.SelectedObstacle >= 0 && _model.SelectedObstacle < m.Obstacles.Count;
            MDSUi.SetActive(obstaclePanel, hasSel && _model.CurrentTool == EditorTool.Obstacle);
            if (hasSel)
            {
                ObstacleDef o = m.Obstacles[_model.SelectedObstacle];
                bool circle = o.Mode == PathMode.Circle;
                MDSUi.SetText(obstacleText, "장애물 " + (_model.SelectedObstacle + 1) + "/" + m.Obstacles.Count
                    + (circle ? "\n원 반지름 " + (o.RadiusHalf * 0.5f).ToString("0.0") + "칸 · 시작 " + (o.StartAngle * 22.5f).ToString("0") + "° · " + (o.Clockwise ? "시계" : "반시계")
                              : "\n경로 지점 " + o.Points.Length + "/" + Rules.MAX_PATH_POINTS + (_pathDrawing ? " — 칸을 눌러 지점 추가" : ""))
                    + "\n속도 " + o.SpeedTier + "단계 · 출발 " + (o.DelayTenths / 10f).ToString("0.0") + "초 뒤");
                MDSUi.Tint(modePingPong, o.Mode == PathMode.PingPong ? MDSUi.Selected : MDSUi.Normal);
                MDSUi.Tint(modeLoop, o.Mode == PathMode.Loop ? MDSUi.Selected : MDSUi.Normal);
                MDSUi.Tint(modeCircle, circle ? MDSUi.Selected : (_model.IsModeUnlocked(PathMode.Circle) ? MDSUi.Normal : MDSUi.Locked));
                for (int i = 0; i < speedButtons.Length; i++) MDSUi.Tint(speedButtons[i], o.SpeedTier == i + 1 ? MDSUi.Selected : MDSUi.Normal);
                MDSUi.SetActive(radiusMinus, circle); MDSUi.SetActive(radiusPlus, circle);
                MDSUi.SetActive(angleMinus, circle); MDSUi.SetActive(anglePlus, circle);
                MDSUi.SetActive(directionButton, circle);
                MDSUi.SetActive(finishPathButton, _pathDrawing);
            }

            var sb = new StringBuilder();
            if (_message.Length > 0) sb.Append(_message).Append('\n');
            ValidationResult v = _model.Validate();
            if (!v.IsValid) sb.Append("고칠 점: ").Append(v.Issues[0].Message).Append(v.Issues.Count > 1 ? " 외 " + (v.Issues.Count - 1) + "개" : "");
            else if (_model.CanUpload) sb.Append("업로드할 수 있어요 — 내 기록: 데스 ").Append(_model.CreatorClear.Deaths).Append(" · ").Append(MDSUi.FormatTicks(_model.CreatorClear.ClearTicks));
            else sb.Append("직접 한 번 깨야 업로드할 수 있어요 (시험 플레이)");
            sb.Append("\n동전 ").Append(m.Coins.Count).Append("/").Append(Rules.MAX_COINS).Append(" · 장애물 ").Append(m.Obstacles.Count).Append("/").Append(Rules.MAX_OBSTACLES);
            MDSUi.SetText(statusText, sb.ToString());
        }

        // ── 입력 ──

        private void Update()
        {
            if (_model == null) return;
            MDSGameDirector d = MDSGameDirector.Instance;
            if (d == null || d.IsPlaying || d.Board == null || d.Board.Map == null) return;

            // 장애물 움직임 미리보기
            _previewTime += Time.unscaledDeltaTime;
            d.Board.RenderObstaclesAtTick(PreviewTick);

            HandleKeys();
            HandleMouse(d.Board);

            if (_dirty)
            {
                _saveTimer += Time.unscaledDeltaTime;
                if (_saveTimer > 2f) SaveDraft();
            }
        }

        private void SaveDraft()
        {
            if (_model == null || !_dirty) return;
            MDSSaveStore.SaveEditorDraft(_model.Map);
            _dirty = false;
            _saveTimer = 0f;
        }

        private void HandleKeys()
        {
            if (nameInput != null && nameInput.isFocused) return;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (ctrl && Input.GetKeyDown(KeyCode.Z))
            {
                if (shift) _model.Redo(); else _model.Undo();
                Rebuild();
                return;
            }
            if (ctrl && Input.GetKeyDown(KeyCode.Y)) { _model.Redo(); Rebuild(); return; }
            if (ctrl) return;

            for (int k = 0; k < 10; k++)
            {
                KeyCode code = k == 9 ? KeyCode.Alpha0 : KeyCode.Alpha1 + k;
                if (!Input.GetKeyDown(code)) continue;
                int index = shift ? 10 + k : k;
                if (index < toolButtons.Length && index <= (int)EditorTool.Eraser) SelectTool((EditorTool)index);
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { _pathDrawing = false; RefreshUi(); }
            if (Input.GetKeyDown(KeyCode.Delete) && _model.SelectedObstacle >= 0) { Do(_model.DeleteObstacle(_model.SelectedObstacle)); _pathDrawing = false; }
            if (Input.GetKeyDown(KeyCode.Backspace) && _pathDrawing) Do(_model.RemoveLastPathPoint());
            if (Input.GetKeyDown(KeyCode.F5)) TestPlay();
        }

        private void HandleMouse(MDSBoardView board)
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Camera cam = board.BoardCamera;
            if (cam == null) return;
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;
            int cx, cy;
            bool inside = board.WorldToCell(world, out cx, out cy);

            if (!_stroking && !overUi && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            {
                bool right = Input.GetMouseButtonDown(1);
                if (_model.CurrentTool == EditorTool.Obstacle)
                {
                    HandleObstacleClick(board, world, inside, cx, cy, right);
                    return;
                }
                if (!inside) return;
                _stroking = true;
                _erasing = right;
                _model.BeginStroke();
                _lastCellX = _lastCellY = -1;
            }

            if (_stroking)
            {
                if (inside && (cx != _lastCellX || cy != _lastCellY))
                {
                    _lastCellX = cx;
                    _lastCellY = cy;
                    EditResult r = _erasing ? _model.Erase(cx, cy) : _model.Apply(cx, cy);
                    if (r != EditResult.NoChange) Do(r);
                }
                if ((_erasing && Input.GetMouseButtonUp(1)) || (!_erasing && Input.GetMouseButtonUp(0)) || (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)))
                {
                    _model.EndStroke();
                    _stroking = false;
                    _erasing = false;
                    RefreshUi();
                }
            }
        }

        private void HandleObstacleClick(MDSBoardView board, Vector3 world, bool inside, int cx, int cy, bool right)
        {
            if (right)
            {
                if (_pathDrawing) { _pathDrawing = false; RefreshUi(); }
                else if (inside)
                {
                    int hit = _model.FindObstacleAt(cx, cy);
                    if (hit >= 0) Do(_model.DeleteObstacle(hit));
                }
                return;
            }
            if (_pathDrawing && _model.SelectedObstacle >= 0)
            {
                Do(_model.AddPathPoint(board.WorldToHalfPoint(world)));
                return;
            }
            if (!inside) return;
            int existing = _model.FindObstacleAt(cx, cy);
            if (existing >= 0)
            {
                _model.SelectedObstacle = existing;
                _pathDrawing = true;
                Rebuild();
                return;
            }
            EditResult r = _model.PlaceObstacle(cx, cy);
            if (r == EditResult.Changed) _pathDrawing = true;
            Do(r);
        }

        // ── 시험 플레이 / 업로드 ──

        private void TestPlay()
        {
            ValidationResult v = _model.Validate();
            if (!v.IsValid)
            {
                _message = "맵을 먼저 고쳐 주세요: " + v.Issues[0].Message;
                RefreshUi();
                return;
            }
            SaveDraft();
            MapEditorModel model = _model;
            MDSGameDirector.Instance.StartEditorTest(model.Map, rec =>
            {
                ReplayResult r = model.SubmitTestPlay(rec);
                _message = r.IsValid ? "직접 깼어요! 업로드할 수 있어요" : "검증 실패: " + r.Verdict;
            });
        }

        private async UniTaskVoid UploadAsync()
        {
            if (!_model.CanUpload || _uploading) return;
            _uploading = true;
            _message = "업로드 중…";
            RefreshUi();
            MapEditorModel.ClearProof proof = _model.CreatorClear;
            MDSGameDirector d = MDSGameDirector.Instance;
            MDSPaletteColors pal = d.Board != null ? d.Board.Palette : MDSPaletteColors.FromCore(Palettes.Get(_model.Map.PaletteId));
            Texture2D preview = MDSMapPreview.Render(_model.Map, pal, 16);
            byte[] png = preview.EncodeToPNG();
            Destroy(preview);
            var req = new WorkshopUploadRequest
            {
                Map = _model.Map.Clone(),
                Title = string.IsNullOrEmpty(_model.Map.Name) ? "이름 없는 맵" : _model.Map.Name,
                Visibility = WorkshopVisibility.Unlisted,
                CreatorDeaths = proof.Deaths,
                CreatorTicks = proof.ClearTicks,
                CreatorRecording = proof.Recording,
                PreviewPng = png,
            };
            WorkshopUploadResult r = await MDSServices.Workshop.UploadAsync(req);
            _uploading = false;
            if (this == null) return;
            if (r.Success)
            {
                MDSUi.CopyToClipboard(r.MapCode);
                _message = "업로드 완료! 맵 코드 " + r.MapCode + " (복사됨)"
                    + (r.AppliedVisibility == WorkshopVisibility.FriendsOnly ? " · 친구만 보기로 올렸어요" : "");
                MDSSaveStore.RecordPlayed(r.MapCode, req.Title, true, proof.Deaths);
                MDSAnalytics.Log("map_upload", 0f, "{\"code\":\"" + r.MapCode + "\"}");
            }
            else
            {
                _message = "업로드 실패: " + r.Error + (r.NeedsLegalAgreement ? " (Steam 창작마당 약관 동의 필요)" : "");
            }
            RefreshUi();
        }
    }
}
