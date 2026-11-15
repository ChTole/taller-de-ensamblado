using System.Collections.Generic;
using AssemblyWorkshop.Joints;
using AssemblyWorkshop.Pieces;
using AssemblyWorkshop.Simulation;
using UnityEditor;
using UnityEngine;

namespace AssemblyWorkshop.Editor
{
    /// <summary>
    /// Validates a whole piece of furniture from Tools → Assembly Workshop → Validate Selected Furniture.
    /// Accepts a prefab, a scene object or a FurnitureDefinition.
    /// </summary>
    public static class FurnitureValidator
    {
        private const string MenuPath = "Tools/Assembly Workshop/Validate Selected Furniture";
        private const string Title = "Validate Furniture";

        [MenuItem(MenuPath)]
        private static void ValidateSelection()
        {
            GameObject root = Selection.activeGameObject;
            if (Selection.activeObject is FurnitureDefinition definition)
            {
                root = definition.PiecesPrefab;
            }

            if (root == null)
            {
                EditorUtility.DisplayDialog(Title, "Select a furniture prefab or its FurnitureDefinition.", "OK");
                return;
            }

            int problems = Validate(root);
            string message = problems == 0
                ? $"'{root.name}': no problems found."
                : $"'{root.name}': {problems} problems found. See the Console for details.";
            EditorUtility.DisplayDialog(Title, message, "OK");
        }

        [MenuItem(MenuPath, true)]
        private static bool CanValidateSelection()
        {
            return Selection.activeGameObject != null || Selection.activeObject is FurnitureDefinition;
        }

        /// <summary>Checks the pieces and joints of the furniture. Returns the number of problems found.</summary>
        public static int Validate(GameObject root)
        {
            int problems = 0;

            void Report(Object context, string message)
            {
                Debug.LogWarning($"[{nameof(FurnitureValidator)}] {message}", context);
                problems++;
            }

            AssemblyPiece[] pieces = root.GetComponentsInChildren<AssemblyPiece>(true);
            if (pieces.Length == 0)
            {
                Report(root, $"'{root.name}' has no pieces (AssemblyPiece).");
            }

            var pieceIds = new HashSet<string>();
            foreach (AssemblyPiece piece in pieces)
            {
                if (!pieceIds.Add(piece.PieceId))
                {
                    Report(piece, $"Piece identifier '{piece.PieceId}' is duplicated.");
                }

                if (piece.transform.parent != null && piece.transform.parent.GetComponentInParent<AssemblyPiece>(true) != null)
                {
                    Report(piece, $"'{piece.PieceId}' is inside another piece: pieces cannot be nested.");
                }

                if ((piece.transform.localScale - Vector3.one).sqrMagnitude > 1e-6f)
                {
                    Report(piece, $"'{piece.PieceId}': the scale of the root object must be (1, 1, 1).");
                }

                if (!AxisAlignment.IsAligned(piece.transform.rotation))
                {
                    Report(piece, $"'{piece.PieceId}': the rotation is not a multiple of 90°.");
                }

                if (piece.GetComponentsInChildren<BoxCollider>(true).Length == 0)
                {
                    Report(piece, $"'{piece.PieceId}' has no Box Colliders.");
                }

                foreach (Collider other in piece.GetComponentsInChildren<Collider>(true))
                {
                    if (!(other is BoxCollider))
                    {
                        Report(other, $"'{other.name}' has a {other.GetType().Name}: only Box Colliders are considered.");
                    }
                }
            }

            JointPoint[] joints = root.GetComponentsInChildren<JointPoint>(true);
            var jointIds = new HashSet<string>();
            int tenons = 0;
            int mortises = 0;

            foreach (JointPoint joint in joints)
            {
                AssemblyPiece owner = joint.GetComponentInParent<AssemblyPiece>(true);

                if (joint.Kind == JointKind.Tenon)
                {
                    tenons++;
                }
                else
                {
                    mortises++;
                }

                if (owner == null)
                {
                    Report(joint, $"'{joint.name}' is not inside a piece.");
                    continue;
                }

                string jointId = joint.JointId;
                if (!jointIds.Add(jointId))
                {
                    Report(joint, $"Joint '{jointId}' is duplicated: give it a unique name within its piece or set 'Joint Id'.");
                }

                if (joint.Length <= 0f)
                {
                    Report(joint, $"'{jointId}': 'Length' must be greater than 0.");
                }

                if (joint.Profile == null)
                {
                    Report(joint, $"'{jointId}' has no 'Profile'.");
                }

                if (joint.GetComponent<Collider>() != null)
                {
                    Report(joint, $"'{jointId}' has a collider: joint points should not have one.");
                }

                if (joint.Kind == JointKind.Tenon && !HasCompatibleMortise(joint, owner, joints))
                {
                    Report(joint, $"Tenon '{jointId}' has no compatible mortise on another piece.");
                }
            }

            if (tenons != mortises)
            {
                Report(root, $"'{root.name}' has {tenons} tenons and {mortises} mortises: it cannot be completed.");
            }

            return problems;
        }

        private static bool HasCompatibleMortise(JointPoint tenon, AssemblyPiece owner, JointPoint[] joints)
        {
            foreach (JointPoint other in joints)
            {
                if (other.Kind == JointKind.Mortise
                    && other.GetComponentInParent<AssemblyPiece>(true) != owner
                    && tenon.IsCompatibleWith(other))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
