using aafccore.control;

namespace aafccore
{
    /// <summary>
    /// Program just passes cmdline args to AppStart class
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            _ = new AppStart(args);
        }

    }
}
