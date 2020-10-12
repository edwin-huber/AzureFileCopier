using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace aafccore.work
{
    interface IWorkItemController
    {
        bool SubmitFile(WorkItem workitem);

        bool SubmitFolder(WorkItem workitem);

        bool SubmitLargeFile(WorkItem workitem);
    }
}
