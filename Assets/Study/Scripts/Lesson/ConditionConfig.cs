namespace Study.Lesson
{
    public enum Condition
    {
        Desktop2D = 1,
        PassiveVR = 2,
        EmbodiedVR = 3,
    }

    // The one place the three conditions are defined. Everything else asks
    // these questions instead of checking the condition number, so the table
    // in the spec maps to exactly these lines.
    //
    //                      Cond 1    Cond 2    Cond 3
    //   display            desktop   Quest 3   Quest 3
    //   learner rotates    no        no        yes
    //   prediction prompt  no        no        yes
    //   feedback           no        no        yes
    //   pause dimming      no        no        yes
    //   explore segment    no        no        yes   (a "sandbox" segment: free gates,
    //                                                  turning and measuring instead of
    //                                                  the segment's scripted events)
    public sealed class ConditionConfig
    {
        public readonly Condition Condition;

        public ConditionConfig(Condition condition)
        {
            Condition = condition;
        }

        public bool UsesHeadset => Condition != Condition.Desktop2D;
        public bool LearnerRotates => Condition == Condition.EmbodiedVR;
        public bool ShowsPredictionPrompt => Condition == Condition.EmbodiedVR;
        public bool ShowsFeedback => Condition == Condition.EmbodiedVR;
        public bool DimsAtPauses => Condition == Condition.EmbodiedVR;
        public bool ExploresSandbox => Condition == Condition.EmbodiedVR;

        // Set once per session (the launch screen does this from allocation.csv in
        // Checkpoint 6). Null until then; the lesson falls back to its dev setting.
        public static ConditionConfig Current { get; set; }

        public override string ToString() => $"C{(int)Condition}";
    }
}
