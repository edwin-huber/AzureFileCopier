using System.Threading.Tasks;

namespace aafccore.work
{
    interface IWork
    {
        public Task StartAsync();
    }
}
