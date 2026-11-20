using AssemblyWorkshop.Common;
using AssemblyWorkshop.Simulation;
using TMPro;
using UnityEngine;

namespace AssemblyWorkshop.UI
{
    /// <summary>
    /// Writes the summary of the last assembly into a text, from SimulationContext.LastResult.
    /// The template and every phrase are editable in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class ResultSummary : MonoBehaviour
    {
        [SerializeField] private TMP_Text summaryText;

        [Tooltip("Placeholders: {furniture}, {status}, {resolution}, {joints}, {total}, {failed}, {helpCount}, {helpTime}, " +
                 "{controlsCount}, {controlsTime}, {time}.")]
        [SerializeField, TextArea(6, 12)] private string template =
            "<b>Mueble:</b> {furniture}\n" +
            "<b>Estado:</b> {status}\n" +
            "<b>Resolución:</b> {resolution}\n" +
            "<b>Uniones:</b> {joints} de {total}\n" +
            "<b>Intentos fallidos:</b> {failed}\n" +
            "<b>Ayuda solicitada:</b> {helpCount} (durante {helpTime})\n" +
            "<b>Tiempo total:</b> {time}";

        [Header("Phrases")]
        [SerializeField] private string completedText = "Finalizado";
        [SerializeField] private string notCompletedText = "No finalizado";
        [SerializeField] private string correctText = "Correcta";
        [SerializeField] private string incorrectText = "Incorrecta";

        [Tooltip("Shown when there is no result, e.g. when entering Play Mode directly in this scene.")]
        [SerializeField, TextArea(2, 4)] private string noResultText = "No hay resultados para mostrar.";

        private void Awake()
        {
            if (!Guard.Required(summaryText, nameof(summaryText), this))
            {
                enabled = false;
            }
        }

        private void Start()
        {
            AssemblyResult result = SimulationContext.LastResult;
            summaryText.text = result != null ? Build(result) : noResultText;
        }

        private string Build(AssemblyResult result)
        {
            FurnitureDefinition furniture = SimulationContext.SelectedFurniture;
            string furnitureName = furniture != null && furniture.FurnitureId == result.FurnitureId
                ? furniture.DisplayName
                : result.FurnitureId;

            return template
                .Replace("{furniture}", furnitureName)
                .Replace("{status}", result.Completed ? completedText : notCompletedText)
                .Replace("{resolution}", result.IsCorrect ? correctText : incorrectText)
                .Replace("{joints}", result.JointsCompleted.ToString())
                .Replace("{total}", result.JointsTotal.ToString())
                .Replace("{failed}", result.FailedAttempts.ToString())
                .Replace("{helpCount}", result.HelpRequests.ToString())
                .Replace("{helpTime}", FormatTime(result.HelpSeconds))
                .Replace("{controlsCount}", result.ControlsViews.ToString())
                .Replace("{controlsTime}", FormatTime(result.ControlsSeconds))
                .Replace("{time}", FormatTime(result.DurationSeconds));
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}