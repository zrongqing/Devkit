//Address class added by the syncfusion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Devkit.Model
{
    public class Address
    {
        
        public string State { get; set; }

        
        public string StreetName { get; set; }

       
        public int DoorNo { get; set; }
        public override string ToString()
        {
            return DoorNo.ToString() + ", " + StreetName + ", " + State;
        }
    }
}
