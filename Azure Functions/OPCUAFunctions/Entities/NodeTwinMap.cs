using System;
using System.Collections.Generic;
using System.Text;

namespace OPCUAFunctions.Entities
{
    public class NodeTwinMap
    {
        public string NodeId { get; set; }
        public string TwinId { get; set; }
        public string Property { get; set; }
        public string ModelId { get; set; }
    }
}
