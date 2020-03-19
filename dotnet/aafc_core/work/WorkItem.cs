namespace aafccore.work
{
    public class WorkItem
    {
        public string TargetPath { get; set; }

        public string SourcePath { get; set; }

        public bool Empty { get; set; } = false;
    }
}
