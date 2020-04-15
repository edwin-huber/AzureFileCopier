using Microsoft.Azure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Text;

namespace aafccore.work
{
    internal class CloudQueueMessageWithStatus
    {
        internal CloudQueueMessage Message;
        internal bool succeeded = false;

        internal CloudQueueMessageWithStatus(CloudQueueMessage msg)
        {
            Message = msg;
        }
    }
}
