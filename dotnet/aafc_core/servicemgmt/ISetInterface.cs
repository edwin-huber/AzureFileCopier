using System.Threading.Tasks;

namespace aafccore.servicemgmt
{
    interface ISetInterface
    {
        Task<bool> Add(string value);

        Task<bool> IsMember(string value);

        Task<bool> Reset();

        // not yet implementing other functions until we really need them
        // Remove
        // Members
    }
}
