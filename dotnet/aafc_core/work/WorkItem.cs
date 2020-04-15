namespace aafccore.work
{
    public class WorkItem
    {
        public string TargetPath { get; set; }

        public string SourcePath { get; set; }

        public bool Empty { get; set; } = false;

        public string Id { get; set; } = "";

        public bool Succeeded { get; set; } = false;
    }
}
