using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF.Logging;
using log4net;
namespace XTMF.Logging
{
    class Logger : ILogger {

        private ILog _log;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        public Logger(Type type)
        {
            _log = LogManager.GetLogger(type);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public Logger(ILog log)
        {
            _log = log;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void Debug(string message)
        {
            _log.Debug(message);
        }

      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exc"></param>
        public void Error(string message, Exception exc)
        {
            _log.Error(message, exc);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void Error(string message)
        {
            _log.Error(message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void Fatal(string message)
        {
            _log.Fatal(message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void Info(string message)
        {
            _log.Info(message);   
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void Warn(string message)
        {
            _log.Warn(message);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Shutdown()
        {
            _log.Logger.Repository.Shutdown();
        }
    }
}
