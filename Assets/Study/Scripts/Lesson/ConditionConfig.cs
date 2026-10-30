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
    //   hands on sphere    yes       no        yes   (place the prediction by moving a
    //                                                  vector, press the gate; mouse in
    //                                                  Cond 1, controllers in Cond 3)
    //
    // Prediction questions, feedback and pause dimming are the same in every
    // condition, so they have no switch here. Cond 2 answers the placement question
    // from a list and watches the turn. Cond 1 vs 3 isolates the display, Cond 2 vs 3
    // the hands-on sphere.
    public sealed class ConditionConfig
    {
        public readonly Condition Condition;

        public ConditionConfig(Condition condition)
        {
            Condition = condition;
        }

        public bool UsesHeadset => Condition != Condition.Desktop2D;
        public bool LearnerRotates => Condition != Condition.PassiveVR;

        // Set once per session (the launch screen does this from allocation.csv in
        // Checkpoint 6). Null until then; the lesson falls back to its dev setting.
        public static ConditionConfig Current { get; set; }

        public override string ToString() => $"C{(int)Condition}";
    }
}
