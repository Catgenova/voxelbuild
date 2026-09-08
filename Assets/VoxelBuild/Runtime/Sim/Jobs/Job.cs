namespace VoxelBuild.Sim.Jobs
{
    public enum JobStatus
    {
        Running,
        Done,
        Failed,
    }

    /// <summary>A unit of colonist behaviour: travel somewhere, do something, finish. Ticked by <see cref="ColonistCore"/>.</summary>
    public abstract class Job
    {
        /// <summary>Short description for the UI ("Mining Stone").</summary>
        public abstract string Label { get; }

        public virtual void OnStart(ColonistCore c) { }
        public abstract JobStatus Tick(ColonistCore c, float dt);
        public virtual void OnEnd(ColonistCore c, JobStatus status) { }
    }
}
