using System;
using System.Collections.Generic;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Interaction;
using AssemblyWorkshop.Joints;
using AssemblyWorkshop.Pieces;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Flow of the simulation scene: loads the chosen furniture, keeps the assembly statistics
    /// and moves to the feedback scene when the user checks the assembly, complete or not.
    /// Each stage is announced with an event.
    /// </summary>
    [DisallowMultipleComponent]
    public class SimulationController : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private AssemblyGraph graph;
        [SerializeField] private JointDetector detector;
        [SerializeField] private JointSnapper snapper;
        [SerializeField] private PieceDragger dragger;
        [SerializeField] private PieceRotator rotator;

        [Header("Furniture")]
        [Tooltip("Object the furniture is instantiated under. Its rotation must be (0, 0, 0).")]
        [SerializeField] private Transform furnitureParent;

        [Tooltip("Furniture loaded when entering Play Mode directly in this scene, without going through the menu.")]
        [SerializeField] private FurnitureDefinition editorFallback;

        [Header("Scenes")]
        [SerializeField] private string feedbackScene = "Feedback";
        [SerializeField] private string menuScene = "MainMenu";

        public event Action<FurnitureDefinition> AssemblyStarted;

        /// <summary>The user checked the assembly. The result says whether every joint was completed.</summary>
        public event Action<AssemblyResult> AssemblyChecked;

        /// <summary>The assembly is restarted. Raised before the scene is reloaded.</summary>
        public event Action<AssemblyResult> AssemblyRestarted;

        /// <summary>Returning to the menu without completing the furniture.</summary>
        public event Action<AssemblyResult> AssemblyAbandoned;

        public FurnitureDefinition Furniture { get; private set; }
        public float ElapsedSeconds => Furniture != null ? Time.time - startTime : 0f;

        private float startTime;
        private bool isRunning;
        private bool isValid;

        private int failedAttempts;
        private int grabs;
        private int rotations;
        private int undos;
        private int detaches;

        private void Awake()
        {
            isValid = Guard.Required(graph, nameof(graph), this)
                & Guard.Required(detector, nameof(detector), this)
                & Guard.Required(snapper, nameof(snapper), this)
                & Guard.Required(dragger, nameof(dragger), this)
                & Guard.Required(rotator, nameof(rotator), this)
                & Guard.Required(furnitureParent, nameof(furnitureParent), this);
        }

        private void OnEnable()
        {
            if (!isValid)
            {
                return;
            }

            detector.AttemptFailed += OnAttemptFailed;
            snapper.Undone += OnUndone;
            snapper.Detached += OnDetached;
            dragger.DragStarted += OnDragStarted;
            rotator.Rotated += OnRotated;
        }

        private void OnDisable()
        {
            if (!isValid)
            {
                return;
            }

            detector.AttemptFailed -= OnAttemptFailed;
            snapper.Undone -= OnUndone;
            snapper.Detached -= OnDetached;
            dragger.DragStarted -= OnDragStarted;
            rotator.Rotated -= OnRotated;
        }

        private void Start()
        {
            if (!isValid)
            {
                return;
            }

            CheckSceneInBuild(feedbackScene, nameof(feedbackScene));
            CheckSceneInBuild(menuScene, nameof(menuScene));

            FurnitureDefinition furniture = SimulationContext.SelectedFurniture != null
                ? SimulationContext.SelectedFurniture
                : editorFallback;

            if (furniture == null)
            {
                Guard.Error(this, "No furniture was chosen in the menu and 'Editor Fallback' is not assigned.");
                return;
            }

            if (furniture.PiecesPrefab == null)
            {
                Guard.Error(this, $"Furniture '{furniture.FurnitureId}' has no 'Pieces Prefab'.");
                return;
            }

            GameObject instance = Instantiate(furniture.PiecesPrefab, furnitureParent);
            if (!graph.Register(instance))
            {
                return;
            }

            Furniture = furniture;
            startTime = Time.time;
            isRunning = true;
            AssemblyStarted?.Invoke(furniture);
        }

        /// <summary>Restarts the assembly of the same furniture by reloading the scene.</summary>
        public void RestartAssembly()
        {
            if (!isRunning)
            {
                return;
            }

            isRunning = false;
            AssemblyRestarted?.Invoke(BuildResult(completed: graph.IsComplete));
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Returns to the menu. If the furniture was not complete, the assembly is marked as abandoned.</summary>
        public void ExitToMenu()
        {
            if (isRunning)
            {
                isRunning = false;
                AssemblyResult result = BuildResult(completed: graph.IsComplete);
                SimulationContext.LastResult = result;
                AssemblyAbandoned?.Invoke(result);
            }

            LoadScene(menuScene);
        }

        /// <summary>
        /// Ends the assembly as it is, complete or not, and loads the feedback scene.
        /// Called from the "Comprobar" button.
        /// </summary>
        public void CheckAssembly()
        {
            if (!isRunning)
            {
                return;
            }

            isRunning = false;
            AssemblyResult result = BuildResult(completed: graph.IsComplete);
            SimulationContext.LastResult = result;
            AssemblyChecked?.Invoke(result);
            LoadScene(feedbackScene);
        }

        private AssemblyResult BuildResult(bool completed)
        {
            return new AssemblyResult
            {
                FurnitureId = Furniture != null ? Furniture.FurnitureId : string.Empty,
                Completed = completed,
                DurationSeconds = ElapsedSeconds,
                JointsCompleted = graph.Connections.Count,
                JointsTotal = graph.TotalJoints,
                FailedAttempts = failedAttempts,
                Grabs = grabs,
                Rotations = rotations,
                Undos = undos,
                Detaches = detaches
            };
        }

        private void OnAttemptFailed(JointFailure failure) => failedAttempts++;
        private void OnUndone(IReadOnlyList<JointPair> pairs) => undos++;
        private void OnDetached(AssemblyPiece piece, IReadOnlyList<JointPair> pairs) => detaches++;
        private void OnDragStarted(PieceGroup group) => grabs++;
        private void OnRotated(PieceGroup group) => rotations++;

        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Guard.Error(this, $"Scene '{sceneName}' is not in the build's scene list.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        private void CheckSceneInBuild(string sceneName, string fieldName)
        {
            if (!string.IsNullOrWhiteSpace(sceneName) && !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Guard.Warning(this, $"'{fieldName}': scene '{sceneName}' is not in File → Build Profiles → Scene List.");
            }
        }
    }
}