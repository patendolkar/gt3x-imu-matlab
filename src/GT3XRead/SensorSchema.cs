using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GT3XRead
{
    public class SensorSchema
    {
        public int ID { get; protected set; }
        public int ColumnsPerRow { get; protected set; }
        public int SamplesInEachRecord { get; protected set; }
        public List<SensorColumn> SensorColumns { get; protected set; }

        public SensorSchema(int id, int columnsPerRow, int samplesInEachRecord)
        {
            ID = id;
            ColumnsPerRow = columnsPerRow;
            SamplesInEachRecord = samplesInEachRecord;
            SensorColumns = new List<SensorColumn>(columnsPerRow);
        }
    }

    public class SensorColumn
    {
        public bool IsBigEndian { get; set; }
        public bool IsSigned { get; set; }

        /// <summary>
        /// Bit offset for the start of this column within the sample.
        /// </summary>
        public byte Offset { get; set; }

        /// <summary>
        /// Bit size of this column within the samples.
        /// </summary>
        public byte Size { get; set; }

        /// <summary>
        /// An SSP encoded floating-point scale factor for the column value. Devide the encoded value by this number to get the output in the proper units.
        /// </summary>
        public double ScaleFactor { get; set; }

        /// <summary>
        /// A fixed-length whitespace-padded ASCII string containing a human-readable name for the column suitable for use in a CSV output.
        /// </summary>
        public string Label { get; set; }
    }

 
}
