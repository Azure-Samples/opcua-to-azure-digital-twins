using System;
using System.Collections.Generic;
using System.Text;

namespace OPCUAFunctions.Entities
{
    public class Node
    {
        public string NodeId { get; set; }
        public string ApplicationUri { get; set; }
        public string DisplayName { get; set; }
        public NodeValue Value { get; set;  }
    }   
    
    public class NodeValue
    {
        public string Value { get; set; }
        public string SourceTimeStamp { get; set; }
    }
}
