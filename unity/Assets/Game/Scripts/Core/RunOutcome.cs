namespace Game.Core
{
    /// <summary>
    /// How a run ended. The string values are the contract with the backend's
    /// `runs.outcome` CHECK constraint - do not rename them casually.
    /// </summary>
    public enum RunOutcome
    {
        Extracted,
        Died,
        Aborted
    }

    public static class RunOutcomeExtensions
    {
        public static string ToWireValue(this RunOutcome outcome) => outcome switch
        {
            RunOutcome.Extracted => "extracted",
            RunOutcome.Died => "died",
            _ => "aborted"
        };
    }
}
