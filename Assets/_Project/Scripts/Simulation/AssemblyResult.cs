namespace AssemblyWorkshop.Simulation
{
    /// <summary>Summary of an assembly, complete or not.</summary>
    public sealed class AssemblyResult
    {
        public string FurnitureId;
        public bool Completed;
        public float DurationSeconds;
        public int JointsCompleted;
        public int JointsTotal;
        public int FailedAttempts;
        public int Grabs;
        public int Rotations;
        public int Undos;
        public int Detaches;
        public int HelpRequests;
        public float HelpSeconds;
    }
}
