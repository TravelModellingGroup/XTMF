
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;
namespace TMG.Frameworks.Data.Loading
{

    public class ArbitraryParameterDataSource<T> : ISetableDataSource<T>
    {
        public bool Loaded
        {
            get { return true; }
        }

        [RunParameter("Data", "", "The initial value")]
        public T Data;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public T GiveData()
        {
            return Data;
        }

        public void LoadData()
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void SetData(T newValue)
        {
            Data = newValue;
        }

        public void UnloadData()
        {
        }
    }

}
