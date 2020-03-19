using System.Threading.Tasks;

namespace aafccore.work
{
    interface IWorkItemMgmt
    {
        Task<bool> WorkAvailable();
        Task<WorkItem> Fetch();

        Task<bool> Submit(WorkItem workitem);

        Task<bool> CompleteWork();
    }
}
