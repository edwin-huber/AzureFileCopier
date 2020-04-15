using System;
using System.Collections.Generic;
using System.Text;

namespace aafccore.control
{
    public enum CopyType
    {
        localtofiles,
        localtoblob
    }
    interface ICopyOptions
    {
        bool Quiet();
        int Workers();
        bool Batch();
        // CopyType GetCopyType();
    }
}
