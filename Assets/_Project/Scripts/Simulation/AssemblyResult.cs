namespace AssemblyWorkshop.Simulation
{
    /// <summary>Summary of an assembly, complete or not.</summary>
    public sealed class AssemblyResult
    {
        /// <summary>Complete and with every joint where the design expects it.</summary>
        public bool IsCorrect => Completed && IncorrectJoints == 0;

        public string FurnitureId;
        public bool Completed;
        public float DurationSeconds;
        public int JointsCompleted;
        public int JointsTotal;

        /// <summary>Joints that fit but are not the ones the design expects.</summary>
        public int IncorrectJoints;
        public int FailedAttempts;
        public int Grabs;
        public int Rotations;
        public int Undos;
        public int Detaches;
        public int HelpRequests;
        public float HelpSeconds;

        /// <summary>Times the controls were consulted during the simulation. Informational, not evaluated.</summary>
        public int ControlsViews;
        public float ControlsSeconds;
    }
}