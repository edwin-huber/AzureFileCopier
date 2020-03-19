using System.Threading.Tasks;

namespace aafccore.servicemgmt
{
    interface IQueueInterface
    {
        Task<string> Dequeue();

        Task Enqueue(string message);

        Task<bool> IsEmpty();

        Task Reset();
    }
}
