using System;
using NeonGrid.Data;
using NeonGrid.Editor.Authoring;
using NeonGrid.Simulation;
using NeonGrid.Validation;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public sealed class NeonGridLevelEditorWindow : EditorWindow
    {
        private enum GridTool
        {
            Select,
            Place,
            Erase
        }

        private enum DeferredGuiAction
        {
            None,
            ChangeAsset,
            CreateAsset,
            Save,
            Resize
        }

        [SerializeField] private LevelDefinition selectedAsset;
        [SerializeField] private int newLevelWidth = 4;
        [SerializeField] private int newLevelHeight = 4;
        [SerializeField] private int requestedWidth = 4;
        [SerializeField] private int requestedHeight = 4;
        [SerializeField] private TileType paletteTileType = TileType.StraightWire;
        [SerializeField] private GridTool gridTool = GridTool.Select;
        [SerializeField] private float cellSize = 72f;

        private LevelAuthoringModel model;
        private GridPosition? selectedCell;
        private Vector2 gridScroll;
        private Vector2 windowScroll;
        private LevelValidationReport lastReport;

        [MenuItem("Neon Grid/Level Editor")]
        public static void Open()
        {
            GetWindow<NeonGridLevelEditorWindow>("Neon Grid Level Editor");
        }

        private void OnEnable()
        {
            saveChangesMessage = "The Neon Grid level has unsaved working-copy changes.";
            Undo.undoRedoPerformed += OnUndoRedo;
            if (selectedAsset != null)
            {
                try
                {
                    LoadModel(selectedAsset);
                }
                catch (ArgumentException exception)
                {
                    Debug.LogError($"Could not restore {selectedAsset.name} in the Level Editor: {exception.Message}",
                        selectedAsset);
                    selectedAsset = null;
                }
            }
            SyncUnsavedFlag();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            DetachModel();
        }

        public override void SaveChanges()
        {
            if (TrySave()) base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            model?.DiscardDirtyFlag();
            base.DiscardChanges();
        }

        private void OnGUI()
        {
            HandleKeyboardShortcuts();
            DeferredGuiAction deferredAction = DeferredGuiAction.None;
            LevelDefinition requestedAsset = selectedAsset;
            using (var scrollView = new EditorGUILayout.ScrollViewScope(windowScroll))
            {
                windowScroll = scrollView.scrollPosition;
                DrawAssetWorkflow(ref deferredAction, out requestedAsset);

                if (model != null)
                {
                    EditorGUILayout.Space();
                    if (DrawResizeControls()) deferredAction = DeferredGuiAction.Resize;
                    EditorGUILayout.Space();
                    DrawToolPalette();
                    EditorGUILayout.Space();
                    DrawGrid();
                    EditorGUILayout.Space();
                    DrawSelectedCellProperties();
                    EditorGUILayout.Space();
                    DrawAnalysisControls();
                    DrawReport();
                }
                else
                {
                    EditorGUILayout.HelpBox("Select an existing LevelDefinition or create a new level asset.",
                        MessageType.Info);
                }
            }

            ProcessDeferredGuiAction(deferredAction, requestedAsset);
        }

        private void DrawAssetWorkflow(ref DeferredGuiAction deferredAction, out LevelDefinition requestedAsset)
        {
            EditorGUILayout.LabelField("Level Asset", EditorStyles.boldLabel);
            requestedAsset = (LevelDefinition)EditorGUILayout.ObjectField(
                "LevelDefinition", selectedAsset, typeof(LevelDefinition), false);
            if (requestedAsset != selectedAsset) deferredAction = DeferredGuiAction.ChangeAsset;

            using (new EditorGUILayout.HorizontalScope())
            {
                newLevelWidth = Mathf.Max(1, EditorGUILayout.IntField("New Width", newLevelWidth));
                newLevelHeight = Mathf.Max(1, EditorGUILayout.IntField("New Height", newLevelHeight));
                if (GUILayout.Button("Create New Asset", GUILayout.Width(130f)))
                    deferredAction = DeferredGuiAction.CreateAsset;
            }

            using (new EditorGUI.DisabledScope(model == null || selectedAsset == null))
            {
                if (GUILayout.Button(model != null && model.HasUnsavedChanges ? "Save Level *" : "Save Level"))
                    deferredAction = DeferredGuiAction.Save;
            }
        }

        private bool DrawResizeControls()
        {
            EditorGUILayout.LabelField("Grid Size", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                requestedWidth = Mathf.Max(1, EditorGUILayout.IntField("Width", requestedWidth));
                requestedHeight = Mathf.Max(1, EditorGUILayout.IntField("Height", requestedHeight));
                return GUILayout.Button("Apply Size", GUILayout.Width(100f));
            }
        }

        private void DrawToolPalette()
        {
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
            gridTool = (GridTool)GUILayout.Toolbar((int)gridTool, new[] { "Select", "Place", "Erase" });

            EditorGUILayout.LabelField("Tile Palette");
            Array values = Enum.GetValues(typeof(TileType));
            const int columns = 4;
            for (int index = 0; index < values.Length; index += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns; column++)
                    {
                        int valueIndex = index + column;
                        if (valueIndex >= values.Length)
                        {
                            GUILayout.FlexibleSpace();
                            continue;
                        }

                        var tileType = (TileType)values.GetValue(valueIndex);
                        bool active = paletteTileType == tileType;
                        if (GUILayout.Toggle(active, FriendlyName(tileType), EditorStyles.miniButton) && !active)
                        {
                            paletteTileType = tileType;
                            gridTool = tileType == TileType.Empty ? GridTool.Erase : GridTool.Place;
                        }
                    }
                }
            }
        }

        private void DrawGrid()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Board Preview", EditorStyles.boldLabel);
                cellSize = EditorGUILayout.Slider("Cell Size", cellSize, 52f, 96f);
            }

            float viewportHeight = Mathf.Min(560f, 34f + model.Height * cellSize);
            GridPosition? clicked;
            using (var scrollView = new EditorGUILayout.ScrollViewScope(gridScroll,
                       GUILayout.Height(viewportHeight)))
            {
                gridScroll = scrollView.scrollPosition;
                clicked = LevelGridPreview.Draw(model, selectedCell, cellSize);
            }

            if (!clicked.HasValue) return;
            selectedCell = clicked;
            switch (gridTool)
            {
                case GridTool.Place:
                    model.Place(clicked.Value, paletteTileType);
                    break;
                case GridTool.Erase:
                    model.Erase(clicked.Value);
                    break;
            }
        }

        private void DrawSelectedCellProperties()
        {
            EditorGUILayout.LabelField("Selected Cell", EditorStyles.boldLabel);
            if (!selectedCell.HasValue)
            {
                EditorGUILayout.HelpBox("Click a grid cell to inspect or edit it.", MessageType.None);
                return;
            }

            GridPosition position = selectedCell.Value;
            LevelCellData cell = model.GetCell(position);
            EditorGUILayout.LabelField("Position", position.ToString());

            TileType type = (TileType)EditorGUILayout.EnumPopup("Tile Type", cell.TileType);
            if (type != cell.TileType)
            {
                model.SetTileType(position, type);
                cell = model.GetCell(position);
            }

            if (cell.TileType == TileType.Empty) return;

            int rotation = EditorGUILayout.IntPopup("Rotation",
                cell.Rotation, new[] { "0°", "90°", "180°", "270°" }, new[] { 0, 1, 2, 3 });
            if (rotation != cell.Rotation)
            {
                model.SetRotation(position, rotation);
                cell = model.GetCell(position);
            }

            if (GUILayout.Button("Rotate Clockwise (R)")) model.RotateClockwise(position);

            if (cell.TileType != TileType.Switch)
            {
                bool rotatable = EditorGUILayout.Toggle("Is Rotatable", cell.IsRotatable);
                if (rotatable != cell.IsRotatable) model.SetRotatable(position, rotatable);
            }
            else
            {
                bool switchOn = EditorGUILayout.Toggle("Starting Switch On", cell.StartingSwitchOn);
                if (switchOn != cell.StartingSwitchOn) model.SetStartingSwitchOn(position, switchOn);
            }
        }

        private void DrawAnalysisControls()
        {
            EditorGUILayout.LabelField("Validation and Solver", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Current Contents"))
                    lastReport = new LevelValidationReport(model.Validate(), null);
                if (GUILayout.Button("Solve / Analyze Current Contents"))
                    lastReport = model.Analyze(PuzzleSolverProfiles.AuthoringExact);
            }
        }

        private void DrawReport()
        {
            if (lastReport == null) return;
            MessageType type = !lastReport.StructuralValidation.IsValid
                ? MessageType.Error
                : lastReport.SolverResult != null && lastReport.SolverResult.Status != PuzzleSolverStatus.Solved
                    ? MessageType.Warning
                    : MessageType.Info;
            string levelName = selectedAsset != null ? selectedAsset.name : "Unsaved Level";
            EditorGUILayout.HelpBox(LevelValidationReportFormatter.Format(levelName, lastReport), type);

            PuzzleSolverResult solver = lastReport.SolverResult;
            if (solver == null || solver.Status != PuzzleSolverStatus.Solved) return;
            for (int index = 0; index < solver.Solution.Count; index++)
            {
                PuzzleAction action = solver.Solution[index];
                if (GUILayout.Button($"Select step {index + 1}: {action}")) selectedCell = action.Position;
            }
        }

        private void ApplyResize()
        {
            LevelResizeImpact impact = model.AnalyzeResize(requestedWidth, requestedHeight);
            bool confirmed = !impact.RequiresDestructiveConfirmation || EditorUtility.DisplayDialog(
                "Confirm destructive resize",
                $"Shrinking will remove {impact.RemovedNonEmptyCells.Count} non-empty tile(s). Continue?",
                "Resize and Remove", "Cancel");
            if (!confirmed) return;

            model.Resize(requestedWidth, requestedHeight, true);
            if (selectedCell.HasValue &&
                (selectedCell.Value.x >= model.Width || selectedCell.Value.y >= model.Height))
                selectedCell = null;
        }

        private void CreateNewAsset()
        {
            if (!ConfirmNavigation()) return;
            string path = EditorUtility.SaveFilePanelInProject("Create Neon Grid Level", "NewLevel", "asset",
                "Choose where to save the new LevelDefinition.", "Assets/NeonGrid/Resources/Levels");
            if (string.IsNullOrEmpty(path)) return;

            LevelDefinition created = LevelAssetPersistence.CreateAsset(path, newLevelWidth, newLevelHeight);
            selectedAsset = created;
            LoadModel(created);
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        private void TryChangeAsset(LevelDefinition candidate)
        {
            if (!ConfirmNavigation()) return;
            if (candidate == null)
            {
                selectedAsset = null;
                DetachModel();
                model = null;
                selectedCell = null;
                lastReport = null;
                SyncUnsavedFlag();
                return;
            }

            try
            {
                LoadModel(candidate);
                selectedAsset = candidate;
            }
            catch (ArgumentException exception)
            {
                EditorUtility.DisplayDialog("Cannot load level", exception.Message, "OK");
            }
        }

        private bool TrySave()
        {
            if (model == null || selectedAsset == null) return false;
            LevelValidationResult result = LevelAssetPersistence.Save(model, selectedAsset);
            lastReport = new LevelValidationReport(result, null);
            if (!result.IsValid)
            {
                EditorUtility.DisplayDialog("Level is not structurally valid",
                    "Fix the validation errors shown in the Level Editor before saving.", "OK");
                return false;
            }

            SyncUnsavedFlag();
            Repaint();
            return true;
        }

        private bool ConfirmNavigation()
        {
            if (model == null || !model.HasUnsavedChanges) return true;
            int choice = EditorUtility.DisplayDialogComplex("Unsaved level changes",
                "Save the current working-copy changes before continuing?", "Save", "Cancel", "Discard");
            UnsavedChangesChoice decision = choice == 0
                ? UnsavedChangesChoice.Save
                : choice == 2
                    ? UnsavedChangesChoice.Discard
                    : UnsavedChangesChoice.Cancel;
            return UnsavedChangesNavigation.CanNavigate(decision, TrySave);
        }

        private void ProcessDeferredGuiAction(DeferredGuiAction action, LevelDefinition requestedAsset)
        {
            if (action == DeferredGuiAction.None) return;

            switch (action)
            {
                case DeferredGuiAction.ChangeAsset:
                    TryChangeAsset(requestedAsset);
                    break;
                case DeferredGuiAction.CreateAsset:
                    CreateNewAsset();
                    break;
                case DeferredGuiAction.Save:
                    TrySave();
                    break;
                case DeferredGuiAction.Resize:
                    ApplyResize();
                    break;
            }

            GUIUtility.ExitGUI();
        }

        private void LoadModel(LevelDefinition level)
        {
            var loadedModel = new LevelAuthoringModel(level.Width, level.Height);
            loadedModel.Load(level);

            DetachModel();
            model = loadedModel;
            model.Changed += OnModelChanged;
            selectedCell = null;
            requestedWidth = model.Width;
            requestedHeight = model.Height;
            lastReport = null;
            SyncUnsavedFlag();
        }

        private void DetachModel()
        {
            if (model != null) model.Changed -= OnModelChanged;
        }

        private void OnModelChanged()
        {
            lastReport = null;
            SyncUnsavedFlag();
            Repaint();
        }

        private void OnUndoRedo()
        {
            if (selectedAsset != null && model != null && !model.HasUnsavedChanges)
                LoadModel(selectedAsset);
        }

        private void SyncUnsavedFlag()
        {
            hasUnsavedChanges = model != null && model.HasUnsavedChanges;
        }

        private void HandleKeyboardShortcuts()
        {
            Event current = Event.current;
            if (model == null || !selectedCell.HasValue || EditorGUIUtility.editingTextField ||
                current.type != EventType.KeyDown || current.keyCode != KeyCode.R) return;

            model.RotateClockwise(selectedCell.Value);
            current.Use();
        }

        private static string FriendlyName(TileType tileType)
        {
            switch (tileType)
            {
                case TileType.TJunction: return "T-Junction";
                case TileType.CrossJunction: return "Cross";
                case TileType.AndGate: return "AND";
                case TileType.OrGate: return "OR";
                default: return ObjectNames.NicifyVariableName(tileType.ToString());
            }
        }
    }
}
