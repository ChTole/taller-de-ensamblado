using System.Collections.Generic;
using AssemblyWorkshop.Common;
using UnityEngine;

namespace AssemblyWorkshop.Simulation
{
    /// <summary>
    /// Ordered list of the available furniture. The menu builds its options from it.
    /// Created from Create → Assembly Workshop → Furniture Catalog.
    /// </summary>
    [CreateAssetMenu(menuName = "Assembly Workshop/Furniture Catalog", fileName = "FurnitureCatalog")]
    public class FurnitureCatalog : ScriptableObject
    {
        [SerializeField] private List<FurnitureDefinition> furniture = new List<FurnitureDefinition>();

        public IReadOnlyList<FurnitureDefinition> Furniture => furniture;

        public FurnitureDefinition FindById(string furnitureId)
        {
            foreach (FurnitureDefinition definition in furniture)
            {
                if (definition != null && definition.FurnitureId == furnitureId)
                {
                    return definition;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            var ids = new HashSet<string>();

            foreach (FurnitureDefinition definition in furniture)
            {
                if (definition == null)
                {
                    Guard.Warning(this, "The furniture list has an empty element.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(definition.FurnitureId) && !ids.Add(definition.FurnitureId))
                {
                    Guard.Warning(this, $"Identifier '{definition.FurnitureId}' is duplicated.");
                }
            }
        }
    }
}
