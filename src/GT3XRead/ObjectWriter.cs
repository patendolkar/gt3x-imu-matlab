using GT3XRead;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class ObjectWriter
{
    public static String[][] WriteSensorPacketsToCsv(List<SensorPacket> sensorPackets)
    {
        // Check if there are any packets to write
        if (sensorPackets == null || sensorPackets.Count == 0)
        {
            Console.WriteLine("SensorPacket null");
            return null;
        }

        List<List<String>> temp = new List<List<String>>();

        // Assuming all packets have the same labels, so we use the first packet to determine header labels
        var labels = sensorPackets[0].Labels.ToList();
        StringBuilder csvContent = new StringBuilder();

        // Create header row
        List<String> headerRow = new List<String> { "Timestamp" };
        headerRow.AddRange(labels);

        temp.Add(headerRow);

        //csvContent.Append("Timestamp,");
        //csvContent.Append(string.Join(",", labels));
        //csvContent.AppendLine();

        // Create each data row
        foreach (var packet in sensorPackets)
        {
            // Add timestamp
            List<String> newRow = new List<String>();

            newRow.Add(packet.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"));
           // csvContent.Append(packet.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"));
           // csvContent.Append(',');

            // Add values corresponding to each label
            foreach (var label in labels)
            {
                // Ensure consistent data order; output 0 if label is not present
                newRow.Add(packet.Values.TryGetValue(label, out double value) ? value.ToString() : "0");
                //csvContent.Append(packet.Values.TryGetValue(label, out double value) ? SanitizeString(value.ToString()) : "0");
                //csvContent.Append(',');
            }
            temp.Add(newRow);
            // Remove the last comma and add a new line
            //csvContent.Length--; // Remove the last comma
            //csvContent.AppendLine();
        }

        String[][] jaggedArray = temp.ConvertAll(row => row.ToArray()).ToArray();
        // Write to file
        return jaggedArray;
    }
}