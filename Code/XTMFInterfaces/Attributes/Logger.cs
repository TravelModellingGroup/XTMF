using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property)]
    public class LoggerAttribute : Attribute
    {
        public string LoggerName { get; set; } = string.Empty;

        /// <summary>
        /// Default Logger Attribute Constructor
        /// </summary>
        public LoggerAttribute()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">Name to assign this logger, otherwise defaults to the IModule type.</param>
        public LoggerAttribute(string name)
        {
            LoggerName = name;
        }
    }
}
