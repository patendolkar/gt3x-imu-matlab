using GT3XRead;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class CSVWriter
{
    public static void WriteSensorPacketsToCsv(string filePath, List<SensorPacket> sensorPackets)
    {
        // Check if there are any packets to write
        if (sensorPackets == null || sensorPackets.Count == 0)
            return;

        // Assuming all packets have the same labels, so we use the first packet to determine header labels
        var labels = sensorPackets[0].Labels.ToList();
        StringBuilder csvContent = new StringBuilder();

        // Create header row
        csvContent.Append("Timestamp,");
        csvContent.Append(string.Join(",", labels));
        csvContent.AppendLine();

        // Create each data row
        foreach (var packet in sensorPackets)
        {
            // Add timestamp
            csvContent.Append(packet.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"));
            csvContent.Append(',');

            // Add values corresponding to each label
            foreach (var label in labels)
            {
                // Ensure consistent data order; output 0 if label is not present
                csvContent.Append(packet.Values.TryGetValue(label, out double value) ? SanitizeString(value.ToString()) : "0");
                csvContent.Append(',');
            }

            // Remove the last comma and add a new line
            csvContent.Length--; // Remove the last comma
            csvContent.AppendLine();
        }

        // Write to file
        File.WriteAllText(filePath, csvContent.ToString());
    }

    public static string SanitizeString(string input)
    {
        return input.Replace("'", "");  
    }
}