using System.Collections.Generic;
using System.Threading.Tasks;

namespace aafccore.work
{
    interface IWorkItemMgmt
    {
        Task<bool> WorkAvailable();
        Task<List<WorkItem>> Fetch();

        Task<bool> Submit(WorkItem workitem);

        Task<bool> CompleteWork();
    }
}
