using aafccore.resources;
using aafccore.servicemgmt;
using Microsoft.Azure.Storage;

namespace aafccore.work
{
    static class WorkItemMgmtFactory
    {
        public static IWorkItemMgmt CreateAzureWorkItemMgmt(string name)
        {
           return new AzureQueueWorkItemMgmt(name, false);
        }
    }
}
