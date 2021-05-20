using System;
using System.Collections.Generic;
using System.Text;

namespace OPCUAFunctions.Entities
{
    public class TwinDto
    {
        public string NodeId { get; set; }
        public string TwinId { get; set; }
        public string ModelId { get; set; }
        public string PropertyName { get; set; }
        public string PropertyValue { get; set; }     
        public DateTime? TimeStamp { get; set; }

        public void Clear() {
            this.TwinId = String.Empty;
            this.PropertyName = String.Empty;
            this.PropertyValue = String.Empty;
            this.TimeStamp = null;
        }
    }    
}
