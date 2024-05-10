using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GT3XRead
{
    public class SensorPacket
    {
        // Properties
        public DateTime Timestamp { get; private set; }
        public IEnumerable<String> Labels { get; private set; } 
        public Dictionary<string, double> Values { get; private set; }
        // Constructor that does not include Value
        public SensorPacket(DateTime timestamp, IEnumerable<String> labels)
        {
            Timestamp = timestamp;
            Labels = labels;
            Values = new Dictionary<string, double>();
            foreach (var label in Labels)
            {
                Values[label] = -1.0;
            }
        }
    }
}
