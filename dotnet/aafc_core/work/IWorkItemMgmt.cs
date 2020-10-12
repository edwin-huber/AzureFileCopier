using System.Collections.Generic;
using System.Threading.Tasks;

namespace aafccore.work
{
    // removed Async to simplify tuning
    interface IWorkItemMgmt
    {
        bool WorkAvailable();
        List<WorkItem> Fetch();

        bool Submit(WorkItem workitem);

        bool CompleteWork();

        int GetCountOfOutstandingWork();
    }
}
