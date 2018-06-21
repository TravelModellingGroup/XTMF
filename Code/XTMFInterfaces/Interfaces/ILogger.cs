using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Logging
{
    public interface ILogger
    {
        /// <summary>
        /// Log Info level messages
        /// </summary>
        /// <param name="context"></param>
        /// <param name="message"></param>
        void Info(IModule context, string message);


    }
}
