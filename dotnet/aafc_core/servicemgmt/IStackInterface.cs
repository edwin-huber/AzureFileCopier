using System.Threading.Tasks;

namespace aafccore.servicemgmt
{
    interface IStackInterface
    {
        Task<string> Pop();

        Task<long> Push(string value);

        Task<bool> IsEmpty();

        Task ResetStack();
    }
}
