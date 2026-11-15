using UnityEngine;

namespace AssemblyWorkshop.Joints
{
    /// <summary>
    /// Joint type. A tenon only fits into a mortise with the same profile.
    /// Created from Create → Assembly Workshop → Joint Profile.
    /// </summary>
    [CreateAssetMenu(menuName = "Assembly Workshop/Joint Profile", fileName = "NewJointProfile")]
    public class JointProfile : ScriptableObject
    {
        [Tooltip("Descriptive name, e.g. 'Tenon 15 × 40 mm'.")]
        [SerializeField] private string displayName;

        [Tooltip("Width and height of the tenon cross-section, in meters. Informational only.")]
        [SerializeField] private Vector2 tenonSection = new Vector2(0.015f, 0.04f);

        [Tooltip("Square cross-section: the tenon fits when rotated by any multiple of 90° around its axis.")]
        [SerializeField] private bool isSquare;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Vector2 TenonSection => tenonSection;
        public bool IsSquare => isSquare;
    }
}
