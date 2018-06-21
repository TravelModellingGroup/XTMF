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
        /// Info message to logger
        /// </summary>
        /// <param name="message"></param>
        void Info(string message);

        /// <summary>
        /// Debug message to logger
        /// </summary>
        /// <param name="message"></param>
        void Debug(string message);
        
        /// <summary>
        /// Warn message to logger
        /// </summary>
        /// <param name="message"></param>
        void Warn(string message);

        /// <summary>
        /// Error message to logger
        /// </summary>
        /// <param name="message"></param>
        void Error(string message);

        /// <summary>
        /// Fatal message to logger
        /// </summary>
        /// <param name="message"></param>
        void Fatal(string message);




    }
}
