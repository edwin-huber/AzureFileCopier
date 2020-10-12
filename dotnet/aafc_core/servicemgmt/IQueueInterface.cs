using System.Threading.Tasks;

namespace aafccore.servicemgmt
{
    interface IQueueInterface
    {
        string Dequeue();

        void Enqueue(string message);

        bool IsEmpty();

        void Reset();
    }
}
