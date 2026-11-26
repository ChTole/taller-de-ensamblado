using System.Collections.Generic;
using AssemblyWorkshop.Common;
using AssemblyWorkshop.Joints;
using AssemblyWorkshop.Pieces;
using UnityEngine;
using UnityEngine.Rendering;

namespace AssemblyWorkshop.Feedback
{
    /// <summary>
    /// Shows a translucent copy of the held group at the pose it will have if released now:
    /// turned and fitted into the mortise. Disappears when there is no valid fit.
    /// </summary>
    [DisallowMultipleComponent]
    public class JointGhost : MonoBehaviour
    {
        [SerializeField] private JointDetector detector;

        [Tooltip("Translucent material for the copy, e.g. URP Unlit with Surface Type Transparent.")]
        [SerializeField] private Material ghostMaterial;

        [Tooltip("The copy is hidden when the group is already this close to its final position, in meters, so it does not overlap the real piece.")]
        [SerializeField, Min(0f)] private float hideDistance = 0.003f;

        [Tooltip("The copy is hidden when the group is already within this angle of its final rotation, in degrees.")]
        [SerializeField, Min(0f)] private float hideAngle = 1f;

        private readonly List<(Transform source, Transform copy)> parts = new List<(Transform, Transform)>();
        private Transform root;
        private PieceGroup builtFor;
        private bool isValid;

        private void Awake()
        {
            isValid = Guard.Required(detector, nameof(detector), this)
                & Guard.Required(ghostMaterial, nameof(ghostMaterial), this);

            if (!isValid)
            {
                enabled = false;
                return;
            }

            root = new GameObject("Joint Ghost").transform;
            root.SetParent(transform, worldPositionStays: false);
            root.gameObject.SetActive(false);
        }

        // Runs after JointDetector updates its candidate in Update
        private void LateUpdate()
        {
            JointMatch match = detector.Current;
            if (match == null || match.MovingGroup == null)
            {
                Hide();
                return;
            }

            if (match.MovingGroup != builtFor)
            {
                Build(match.MovingGroup);
            }

            Vector3 delta = match.LateralOffset + match.AxialOffset;
            float angle = Quaternion.Angle(Quaternion.identity, match.Rotation);
            if (delta.magnitude <= hideDistance && angle <= hideAngle)
            {
                root.gameObject.SetActive(false);
                return;
            }

            // Same transformation as the fit: turn about the pivot, then translate
            foreach ((Transform source, Transform copy) in parts)
            {
                if (source == null)
                {
                    continue;
                }

                Vector3 position = match.Pivot + match.Rotation * (source.position - match.Pivot) + delta;
                copy.SetPositionAndRotation(position, match.Rotation * source.rotation);
                copy.localScale = source.lossyScale;
            }

            root.gameObject.SetActive(true);
        }

        private void Build(PieceGroup group)
        {
            Clear();
            builtFor = group;

            foreach (AssemblyPiece piece in group.Pieces)
            {
                foreach (Renderer pieceRenderer in piece.Renderers)
                {
                    if (!(pieceRenderer is MeshRenderer) || !pieceRenderer.TryGetComponent(out MeshFilter filter)
                        || filter.sharedMesh == null)
                    {
                        continue;
                    }

                    var copyObject = new GameObject(pieceRenderer.name);
                    copyObject.transform.SetParent(root, worldPositionStays: false);
                    copyObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

                    MeshRenderer copyRenderer = copyObject.AddComponent<MeshRenderer>();
                    var materials = new Material[filter.sharedMesh.subMeshCount];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = ghostMaterial;
                    }

                    copyRenderer.sharedMaterials = materials;
                    copyRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    copyRenderer.receiveShadows = false;
                    parts.Add((pieceRenderer.transform, copyObject.transform));
                }
            }
        }

        private void Hide()
        {
            if (builtFor != null)
            {
                Clear();
            }
        }

        private void Clear()
        {
            foreach ((Transform source, Transform copy) part in parts)
            {
                if (part.copy != null)
                {
                    Destroy(part.copy.gameObject);
                }
            }

            parts.Clear();
            builtFor = null;

            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }
    }
}