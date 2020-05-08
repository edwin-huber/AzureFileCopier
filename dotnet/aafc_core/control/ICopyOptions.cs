using System;
using System.Collections.Generic;
using System.Text;

namespace aafccore.control
{
    interface ICopyOptions
    {
        bool Quiet();
        int Workers();
        bool Batch();
        int NumFileRunnersPerQueue();
    }
}
