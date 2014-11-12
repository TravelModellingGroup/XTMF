using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;

using System.Data;
using System.Data.Sql;

namespace Tasha.Estimation
{
    [ModuleInformation(Description = "This module is designed to take the ")]
    public class TransformTripsToIncludeAccessMode : ISelfContainedModule
    {
        public string Name { get; set; }

        private Func<float> _Progress = () => 0f;

        public float Progress { get { return _Progress(); } }

        [SubModelInformation(Required = true, Description = "The connection to the database.")]
        public IResource DatabaseConnection;

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return new Tuple<byte, byte, byte>( 50, 150, 50 );
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            var dbConnection = this.DatabaseConnection.AquireResource<IDbConnection>();
            if ( dbConnection == null )
            {
                throw new XTMFRuntimeException( "In '" + this.Name + "' we were unable to get a database connection!" );
            }
            using (var command = dbConnection.CreateCommand())
            {
                
            }
        }
    }
}
