using System.Text.RegularExpressions;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Pieces;
using UnityEngine;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Data for a piece of furniture: what the menu shows and the prefab with its pieces.
    /// Created from Create → Assembly Workshop → Furniture.
    /// </summary>
    [CreateAssetMenu(menuName = "Assembly Workshop/Furniture", fileName = "NewFurniture")]
    public class FurnitureDefinition : ScriptableObject
    {
        private static readonly Regex IdPattern = new Regex("^[a-z0-9_]+$");

        [Tooltip("Stable identifier used in telemetry, e.g. coffee_table. Do not change it once published.")]
        [SerializeField] private string furnitureId;

        [Tooltip("Name shown to the user.")]
        [SerializeField] private string displayName;

        [SerializeField, Range(1, 3)] private int difficulty = 1;

        [Tooltip("Description shown to the user.")]
        [SerializeField, TextArea(3, 8)] private string description;

        [Tooltip("Image of the assembled furniture, used in the menu and in the help view.")]
        [SerializeField] private Sprite thumbnail;

        [Tooltip("Prefab with every piece in its starting position on the work surface.")]
        [SerializeField] private GameObject piecesPrefab;

        public string FurnitureId => furnitureId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int Difficulty => difficulty;
        public string Description => description;
        public Sprite Thumbnail => thumbnail;
        public GameObject PiecesPrefab => piecesPrefab;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(furnitureId))
            {
                Guard.Warning(this, $"'{name}' has no 'Furniture Id'.");
            }
            else if (!IdPattern.IsMatch(furnitureId))
            {
                Guard.Warning(this, $"'{name}': 'Furniture Id' should only use lowercase letters, digits and underscores, e.g. coffee_table.");
            }

            if (piecesPrefab == null)
            {
                Guard.Warning(this, $"'{name}' has no 'Pieces Prefab'.");
            }
            else if (piecesPrefab.GetComponentInChildren<AssemblyPiece>(true) == null)
            {
                Guard.Warning(this, $"'{name}': prefab '{piecesPrefab.name}' has no pieces (AssemblyPiece).");
            }
        }
    }
}
