using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace aafccore.work
{
    interface IWorkItemController
    {
        Task<bool> SubmitFile(WorkItem workitem);

        Task<bool> SubmitFolder(WorkItem workitem);

        Task<bool> SubmitLargeFile(WorkItem workitem);
    }
}
