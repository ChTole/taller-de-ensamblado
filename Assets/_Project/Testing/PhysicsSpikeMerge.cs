using UnityEngine;

namespace AssemblyWorkshop.Testing
{
    /// <summary>
    /// Physics spike only. Simulates what AssemblyGraph does when two groups merge:
    /// the piece of the second group moves under the first group's rigidbody,
    /// and the second group is destroyed. Delete this script once the spike is done.
    /// </summary>
    public class PhysicsSpikeMerge : MonoBehaviour
    {
        [Header("Groups")]
        [Tooltip("Group that keeps its rigidbody and receives the piece.")]
        [SerializeField] private Rigidbody targetGroup;

        [Tooltip("Group that is dissolved after the merge.")]
        [SerializeField] private Rigidbody sourceGroup;

        [Tooltip("Piece of the source group that moves to the target group.")]
        [SerializeField] private Transform piece;

        [Header("Interaction refresh")]
        [Tooltip("Disable and enable these objects after the merge (the ISDK interaction children of the target group). Leave empty to test without refreshing.")]
        [SerializeField] private GameObject[] refreshAfterMerge;

        [Tooltip("Controller button that triggers the merge.")]
        [SerializeField] private OVRInput.Button mergeButton = OVRInput.Button.One;

        [Tooltip("Seconds after start to merge automatically, for tests with hands only. Zero disables it.")]
        [SerializeField] private float autoMergeDelay = 0f;

        private bool merged;

        private void Start()
        {
            if (autoMergeDelay > 0f)
            {
                Invoke(nameof(Merge), autoMergeDelay);
            }
        }

        private void Update()
        {
            if (!merged && OVRInput.GetDown(mergeButton))
            {
                Merge();
            }
        }

        [ContextMenu("Merge")]
        public void Merge()
        {
            if (merged || targetGroup == null || sourceGroup == null || piece == null)
            {
                Debug.LogWarning("[PhysicsSpikeMerge] Missing references or already merged.");
                return;
            }

            merged = true;
            piece.SetParent(targetGroup.transform, true);
            Destroy(sourceGroup.gameObject);

            foreach (GameObject item in refreshAfterMerge)
            {
                if (item == null) continue;
                item.SetActive(false);
                item.SetActive(true);
            }

            Debug.Log($"[PhysicsSpikeMerge] '{piece.name}' merged into '{targetGroup.name}'.");
        }
    }
}