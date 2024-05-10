using System.Text;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

namespace GT3XRead
{
    public class GT3XParser
    { 
        public static String[][] ProcessData(string filepath)
        {
            Dictionary<int, SensorSchema> _sensorSchemata = new Dictionary<int, SensorSchema>();
            Dictionary<int, List<SensorPacket>> SensorPacketsAll = new Dictionary<int, List<SensorPacket>>();

            FileInfo fi = new FileInfo(filepath);

            Console.WriteLine("Extracting Bin...");
            var tempZipDirectoryPath = $"{fi.DirectoryName}\\outputFromGT3XFileFixerLinqPad";
            ClearTempDirectory(tempZipDirectoryPath);

            // Unzip files within GT3X to temp directory
            using (Ionic.Zip.ZipFile zipFile = Ionic.Zip.ZipFile.Read(filepath)) { zipFile.ExtractAll(tempZipDirectoryPath); }
            Console.WriteLine("Done. Reading...");
            // Read Gt3x events/records
            using (var logReader = new LogBinFileReader($"{tempZipDirectoryPath}\\log.bin"))
            {
                while (logReader._gt3xReader.Position < logReader._logBinLength)
                {
                    try
                    {
                        Gt3xRawEventModel rawEvent = new Gt3xRawEventModel();
                        if (logReader.TryReadEvent(out rawEvent))
                        {
                            byte eventType = rawEvent.LogRecordHeader.EventType;
                            ushort size = rawEvent.LogRecordHeader.Size;
                            uint timestamp = rawEvent.LogRecordHeader.Timestamp;

                            // Convert Unix timestamp to DateTime
                            DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;

                            // Print values
                            /*Console.WriteLine("Event Type (byte): 0x" + eventType.ToString("X2"));
                            Console.WriteLine($"Size: {size}");
                            Console.WriteLine($"Timestamp: {timestamp} (Unix)");
                            Console.WriteLine($"Readable Date: {dateTime}");*/

                            // Optionally, to display in local time if needed
                            DateTime localDateTime = dateTime.ToLocalTime();
                            //Console.WriteLine($"Local Date Time: {localDateTime}");


                            switch (eventType)
                            {
                                case 0x06:
                                    //Console.WriteLine("Event Type: Metadata");
                                    break;
                                case 0x18:
                                    //Console.WriteLine("Event Type: Sensor Schema");
                                    int id = BitConverter.ToInt16(rawEvent.Payload, 0);
                                    int columns = BitConverter.ToInt16(rawEvent.Payload, 2);
                                    int samples = BitConverter.ToInt16(rawEvent.Payload, 4);
                                    var sensorSchema = new SensorSchema(id, columns, samples);

                                    const int BYTES_PER_COLUMN = 23;
                                    for (int i = 0; i < columns; i++)
                                    {
                                        int startingOffset = 6 + (BYTES_PER_COLUMN * i);
                                        byte columnFlags = rawEvent.Payload[startingOffset];

                                        bool bigEndian = ((byte)(columnFlags & (1 << 0)) != 0);
                                        bool signed = ((byte)(columnFlags & (1 << 1)) != 0);

                                        byte columnOffset = rawEvent.Payload[startingOffset + 1];
                                        byte columnSize = rawEvent.Payload[startingOffset + 2];
                                        uint value = BitConverter.ToUInt32(rawEvent.Payload, startingOffset + 3);

                                        double columnScaleFactor = Math.Round(DeviceUtilities.SspCodec.Decode(value), 6,
                                            MidpointRounding.AwayFromZero);

                                        var columnLabel = Encoding.ASCII.GetString(rawEvent.Payload, startingOffset + 7, 16).Trim();
                                        SensorColumn column = new SensorColumn
                                        {
                                            IsBigEndian = bigEndian,
                                            IsSigned = signed,
                                            Offset = columnOffset,
                                            Size = columnSize,
                                            ScaleFactor = columnScaleFactor,
                                            Label = columnLabel
                                        };

                                        sensorSchema.SensorColumns.Add(column);
                                    }
                                    _sensorSchemata.Add(id, sensorSchema);
                                    SensorPacketsAll.Add(id, new List<SensorPacket>());
                                    break;
                                case 0x19:
                                    //Console.WriteLine("Event Type: Sensor Data");
                                    int schemaId = BitConverter.ToInt16(rawEvent.Payload, 0);
                                    if (!_sensorSchemata.ContainsKey(schemaId))
                                        throw new ArgumentException("Sensor schemata doesn't have entry for ID #" + schemaId);

                                    List<SensorPacket> SensorPackets = new List<SensorPacket>(100);

                                    SensorSchema schema = _sensorSchemata[schemaId];

                                    const int BITS_PER_BYTE = 8;
                                    int offset = 2;

                                    //Ticker tick = new Ticker(schema.SamplesInEachRecord); //this helps calculate timestamps
                                    long rawSecondCounter = 0;

                                    // DateTime firstSample = rawEvent.LogRecordHeader.TimeStamp.Date +
                                    //                        new TimeSpan(TimeStamp.Hour,
                                    //                            TimeStamp.Minute, TimeStamp.Second);

                                    DateTime firstSample = dateTime;
                                    int totalBytesPerSample = GetTotalBytesPerSample(schema);

                                    //for (int i = 0; i < schema.SamplesInEachRecord; i++)
                                    int sampsPerRecord = rawEvent.Payload.Length / totalBytesPerSample;
                                    //Console.WriteLine(rawEvent.Payload.Length / sampsPerRecord);
                                    if ((rawEvent.Payload.Length / totalBytesPerSample) < 100)
                                    {
                                        sampsPerRecord = (rawEvent.Payload.Length / totalBytesPerSample);
                                    }
                                    else
                                    {
                                        sampsPerRecord = 100;
                                    }
                                    //sampsPerRecord = 100;
                                    for (int i = 0; i < sampsPerRecord; i++)
                                    {
                                        SensorPacket sensorPacket =
                                           new SensorPacket(firstSample.AddMilliseconds(rawSecondCounter),
                                                schema.SensorColumns.Select(x => x.Label));

                                        foreach (SensorColumn column in schema.SensorColumns)
                                        {
                                            //get bytes
                                            short sensorValue = 0;

                                            int bytesInValue = column.Size / BITS_PER_BYTE;

                                            if (column.IsSigned)
                                            {
                                                if (bytesInValue == 1)
                                                    sensorValue = rawEvent.Payload[offset];
                                                else if (bytesInValue == 2)
                                                    sensorValue = BitConverter.ToInt16(rawEvent.Payload, offset);
                                                else
                                                {
                                                    //unable to parse
                                                }
                                            }
                                            else
                                            {
                                                if (bytesInValue == 1)
                                                {
                                                    sensorValue = rawEvent.Payload[offset];
                                                }
                                                else if (bytesInValue == 2)
                                                {
                                                    var uintvalue = BitConverter.ToUInt16(rawEvent.Payload, offset);
                                                    sensorValue = (short)uintvalue;
                                                }
                                                else
                                                {
                                                    //unable to parse
                                                }
                                            }

                                            if (column.IsBigEndian)
                                                sensorValue = (short)((sensorValue << 8) | ((sensorValue >> 8) & 0xFF));

                                            double result = column.ScaleFactor != 0 ? sensorValue / column.ScaleFactor : sensorValue;

                                            if (column.Label.Equals("temperature", StringComparison.CurrentCultureIgnoreCase))
                                                result += 21;

                                            //only add values for non-empty column names
                                            if (!string.IsNullOrEmpty(column.Label))
                                                sensorPacket.Values[column.Label] = result;
                                            //sensorPacket.value = result;


                                            offset += (column.Size / BITS_PER_BYTE);
                                        }
                                        rawSecondCounter += 10;
                                        SensorPacketsAll[schemaId].Add(sensorPacket);
                                    }
                                    break;
                                default:
                                    //Console.WriteLine("Event Type: Unknown");
                                    break;
                            }
                            //Console.ReadLine();
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }
                }
            }
            
                var obj = ObjectWriter.WriteSensorPacketsToCsv(SensorPacketsAll[1]);
               

                Console.WriteLine("Done.");
                
            

            return obj;
        }

        public static void WriteGT3XToCSV(string filepath, string outfilepath)
        {
            Dictionary<int, SensorSchema> _sensorSchemata = new Dictionary<int, SensorSchema>();
            Dictionary<int, List<SensorPacket>> SensorPacketsAll = new Dictionary<int, List<SensorPacket>>();

            FileInfo fi = new FileInfo(filepath);

            Console.WriteLine("Extracting Bin...");
            var tempZipDirectoryPath = $"{fi.DirectoryName}\\outputFromGT3XFileFixerLinqPad";
            ClearTempDirectory(tempZipDirectoryPath);

            // Unzip files within GT3X to temp directory
            using (Ionic.Zip.ZipFile zipFile = Ionic.Zip.ZipFile.Read(filepath)) { zipFile.ExtractAll(tempZipDirectoryPath); }
            Console.WriteLine("Done. Reading...");
            // Read Gt3x events/records
            using (var logReader = new LogBinFileReader($"{tempZipDirectoryPath}\\log.bin"))
            {
                while (logReader._gt3xReader.Position < logReader._logBinLength)
                {
                    try
                    {
                        Gt3xRawEventModel rawEvent = new Gt3xRawEventModel();
                        if (logReader.TryReadEvent(out rawEvent))
                        {
                            byte eventType = rawEvent.LogRecordHeader.EventType;
                            ushort size = rawEvent.LogRecordHeader.Size;
                            uint timestamp = rawEvent.LogRecordHeader.Timestamp;

                            // Convert Unix timestamp to DateTime
                            DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;

                            // Print values
                            /*Console.WriteLine("Event Type (byte): 0x" + eventType.ToString("X2"));
                            Console.WriteLine($"Size: {size}");
                            Console.WriteLine($"Timestamp: {timestamp} (Unix)");
                            Console.WriteLine($"Readable Date: {dateTime}");*/

                            // Optionally, to display in local time if needed
                            DateTime localDateTime = dateTime.ToLocalTime();
                            //Console.WriteLine($"Local Date Time: {localDateTime}");


                            switch (eventType)
                            {
                                case 0x06:
                                    //Console.WriteLine("Event Type: Metadata");
                                    break;
                                case 0x18:
                                    //Console.WriteLine("Event Type: Sensor Schema");
                                    int id = BitConverter.ToInt16(rawEvent.Payload, 0);
                                    int columns = BitConverter.ToInt16(rawEvent.Payload, 2);
                                    int samples = BitConverter.ToInt16(rawEvent.Payload, 4);
                                    var sensorSchema = new SensorSchema(id, columns, samples);

                                    const int BYTES_PER_COLUMN = 23;
                                    for (int i = 0; i < columns; i++)
                                    {
                                        int startingOffset = 6 + (BYTES_PER_COLUMN * i);
                                        byte columnFlags = rawEvent.Payload[startingOffset];

                                        bool bigEndian = ((byte)(columnFlags & (1 << 0)) != 0);
                                        bool signed = ((byte)(columnFlags & (1 << 1)) != 0);

                                        byte columnOffset = rawEvent.Payload[startingOffset + 1];
                                        byte columnSize = rawEvent.Payload[startingOffset + 2];
                                        uint value = BitConverter.ToUInt32(rawEvent.Payload, startingOffset + 3);

                                        double columnScaleFactor = Math.Round(DeviceUtilities.SspCodec.Decode(value), 6,
                                            MidpointRounding.AwayFromZero);

                                        var columnLabel = Encoding.ASCII.GetString(rawEvent.Payload, startingOffset + 7, 16).Trim();
                                        SensorColumn column = new SensorColumn
                                        {
                                            IsBigEndian = bigEndian,
                                            IsSigned = signed,
                                            Offset = columnOffset,
                                            Size = columnSize,
                                            ScaleFactor = columnScaleFactor,
                                            Label = columnLabel
                                        };

                                        sensorSchema.SensorColumns.Add(column);
                                    }
                                    _sensorSchemata.Add(id, sensorSchema);
                                    SensorPacketsAll.Add(id, new List<SensorPacket>());
                                    break;
                                case 0x19:
                                    //Console.WriteLine("Event Type: Sensor Data");
                                    int schemaId = BitConverter.ToInt16(rawEvent.Payload, 0);
                                    if (!_sensorSchemata.ContainsKey(schemaId))
                                        throw new ArgumentException("Sensor schemata doesn't have entry for ID #" + schemaId);

                                    List<SensorPacket> SensorPackets = new List<SensorPacket>(100);

                                    SensorSchema schema = _sensorSchemata[schemaId];

                                    const int BITS_PER_BYTE = 8;
                                    int offset = 2;

                                    //Ticker tick = new Ticker(schema.SamplesInEachRecord); //this helps calculate timestamps
                                    long rawSecondCounter = 0;

                                    // DateTime firstSample = rawEvent.LogRecordHeader.TimeStamp.Date +
                                    //                        new TimeSpan(TimeStamp.Hour,
                                    //                            TimeStamp.Minute, TimeStamp.Second);

                                    DateTime firstSample = dateTime;
                                    int totalBytesPerSample = GetTotalBytesPerSample(schema);

                                    //for (int i = 0; i < schema.SamplesInEachRecord; i++)
                                    int sampsPerRecord = rawEvent.Payload.Length / totalBytesPerSample;
                                    //Console.WriteLine(rawEvent.Payload.Length / sampsPerRecord);
                                    if ((rawEvent.Payload.Length / totalBytesPerSample) < 100)
                                    {
                                        sampsPerRecord = (rawEvent.Payload.Length / totalBytesPerSample);
                                    }
                                    else
                                    {
                                        sampsPerRecord = 100;
                                    }
                                    //sampsPerRecord = 100;
                                    for (int i = 0; i < sampsPerRecord; i++)
                                    {
                                        SensorPacket sensorPacket =
                                           new SensorPacket(firstSample.AddMilliseconds(rawSecondCounter),
                                                schema.SensorColumns.Select(x => x.Label));

                                        foreach (SensorColumn column in schema.SensorColumns)
                                        {
                                            //get bytes
                                            short sensorValue = 0;

                                            int bytesInValue = column.Size / BITS_PER_BYTE;

                                            if (column.IsSigned)
                                            {
                                                if (bytesInValue == 1)
                                                    sensorValue = rawEvent.Payload[offset];
                                                else if (bytesInValue == 2)
                                                    sensorValue = BitConverter.ToInt16(rawEvent.Payload, offset);
                                                else
                                                {
                                                    //unable to parse
                                                }
                                            }
                                            else
                                            {
                                                if (bytesInValue == 1)
                                                {
                                                    sensorValue = rawEvent.Payload[offset];
                                                }
                                                else if (bytesInValue == 2)
                                                {
                                                    var uintvalue = BitConverter.ToUInt16(rawEvent.Payload, offset);
                                                    sensorValue = (short)uintvalue;
                                                }
                                                else
                                                {
                                                    //unable to parse
                                                }
                                            }

                                            if (column.IsBigEndian)
                                                sensorValue = (short)((sensorValue << 8) | ((sensorValue >> 8) & 0xFF));

                                            double result = column.ScaleFactor != 0 ? sensorValue / column.ScaleFactor : sensorValue;

                                            if (column.Label.Equals("temperature", StringComparison.CurrentCultureIgnoreCase))
                                                result += 21;

                                            //only add values for non-empty column names
                                            if (!string.IsNullOrEmpty(column.Label))
                                                sensorPacket.Values[column.Label] = result;
                                            //sensorPacket.value = result;


                                            offset += (column.Size / BITS_PER_BYTE);
                                        }
                                        rawSecondCounter += 10;
                                        SensorPacketsAll[schemaId].Add(sensorPacket);
                                    }
                                    break;
                                default:
                                    //Console.WriteLine("Event Type: Unknown");
                                    break;
                            }
                            //Console.ReadLine();
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }
                }
            }

            CSVWriter.WriteSensorPacketsToCsv(outfilepath, SensorPacketsAll[1]);
            Console.WriteLine("Done.");

        }
        private static void ClearTempDirectory(string dirName)
        {
            System.IO.DirectoryInfo di = new DirectoryInfo(dirName);
            if (!di.Exists) di.Create();
            foreach (FileInfo file in di.GetFiles()) { file.Delete(); }
            foreach (DirectoryInfo dir in di.GetDirectories()) { dir.Delete(true); }
        }

        private static int GetTotalBytesPerSample(SensorSchema schema)
        {
            int sumSize = schema.SensorColumns.Sum(column => column.Size);
            return sumSize / 8;
        }

        static DateTime? ConvertUnixTimestampToDateTime(string timestamp)
        {
            if (long.TryParse(timestamp, out long unixTime))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
            }
            return null;
        }
    }

    public class Gt3xRawEventHeader
    {
        static byte[] validEventTypes = new byte[27] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A };

        public byte EventType
        {
            get;
            set;
        }

        public uint Timestamp
        {
            get;
            set;
        }

        public ushort Size
        {
            get;
            set;
        }

        public bool HeaderIsValid()
        {
            return true;
        }

        public bool IsValidHeader()
        {
            return IsValidEventType() && IsValidTimestamp();
        }

        private bool IsValidEventType()
        {
            return validEventTypes.Contains(EventType);
        }

        private bool IsValidTimestamp()
        {
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(Timestamp);

            if (dateTimeOffset == null) return false;

            // not valid date in future
            if (dateTimeOffset.DateTime > DateTime.UtcNow) return false;

            // not valid if too far in the past
            if (dateTimeOffset.DateTime < DateTime.UtcNow.AddYears(-10)) return false;

            return true;
        }
    }

    public class Gt3xRawEventModel
    {
        public static readonly byte LogSeparatorValue = 30;

        public byte Separator
        {
            get;
            set;
        }

        public Gt3xRawEventHeader LogRecordHeader;

        public byte[] Payload
        {
            get;
            set;
        }

        public byte Checksum
        {
            get;
            set;
        }

        public bool ChecksumIsValid => CalculateChecksum() == Checksum;

        public byte CalculateChecksum()
        {
            byte separator = Separator;
            separator = (byte)(separator ^ LogRecordHeader.EventType);
            separator = (byte)(separator ^ (byte)(LogRecordHeader.Timestamp & 0xFF));
            separator = (byte)(separator ^ (byte)((LogRecordHeader.Timestamp >> 8) & 0xFF));
            separator = (byte)(separator ^ (byte)((LogRecordHeader.Timestamp >> 16) & 0xFF));
            separator = (byte)(separator ^ (byte)((LogRecordHeader.Timestamp >> 24) & 0xFF));
            separator = (byte)(separator ^ (byte)(LogRecordHeader.Size & 0xFF));
            separator = (byte)(separator ^ (byte)((LogRecordHeader.Size >> 8) & 0xFF));
            separator = Payload.Aggregate(separator, (byte current, byte t) => (byte)(current ^ t));
            return (byte)(~separator);
        }

    }

    public struct Gt3xRawEvent
    {
        public static readonly byte LogSeparatorValue = 30;

        public byte Separator
        {
            get;
            set;
        }

        public byte EventType
        {
            get;
            set;
        }

        public uint Timestamp
        {
            get;
            set;
        }

        public ushort Size
        {
            get;
            set;
        }

        public byte[] Payload
        {
            get;
            set;
        }

        public byte Checksum
        {
            get;
            set;
        }

        public bool ChecksumIsValid => CalculateChecksum() == Checksum;

        public byte CalculateChecksum()
        {
            byte separator = Separator;
            separator = (byte)(separator ^ EventType);
            separator = (byte)(separator ^ (byte)(Timestamp & 0xFF));
            separator = (byte)(separator ^ (byte)((Timestamp >> 8) & 0xFF));
            separator = (byte)(separator ^ (byte)((Timestamp >> 16) & 0xFF));
            separator = (byte)(separator ^ (byte)((Timestamp >> 24) & 0xFF));
            separator = (byte)(separator ^ (byte)(Size & 0xFF));
            separator = (byte)(separator ^ (byte)((Size >> 8) & 0xFF));
            separator = Payload.Aggregate(separator, (byte current, byte t) => (byte)(current ^ t));
            return (byte)(~separator);
        }
    }

    public class Gt3xStreamReader : IDisposable
    {
        private readonly BinaryReader _stream;

        private const int HeaderLength = 9;

        public long Position
        {
            get;
            private set;
        }

        public List<string> ReadErrors
        {
            get;
        } = new List<string>();


        public Gt3xStreamReader(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            _stream = new BinaryReader(stream);
        }

        private bool IsValidEventType(byte eventType)
        {
            return true;
        }

        private bool IsValidTimestamp(UInt32 timestamp)
        {
            return true;
        }

        public bool TrySeekToNextValiddRecordHeader(out Gt3xRawEventHeader eventHeader)
        {
            bool foundValidHeader = false;
            eventHeader = null;
            try
            {
                byte b = _stream.ReadByte(); Position += 1;
                eventHeader = new Gt3xRawEventHeader();
                while (!foundValidHeader)
                {
                    if (b == Gt3xRawEventModel.LogSeparatorValue)
                    {
                        eventHeader.EventType = _stream.ReadByte(); Position += 1;
                        eventHeader.Timestamp = _stream.ReadUInt32(); Position += 4;
                        eventHeader.Size = _stream.ReadUInt16(); Position += 2;

                        if (eventHeader.IsValidHeader())
                        {
                            // reset position back to sync byte (or separator value) since valid log header is found
                            foundValidHeader = true;
                            Position += -8;
                            _stream.BaseStream.Seek(Position, SeekOrigin.Begin);
                            return true;
                        }
                        else
                        {
                            Position += -7;
                            _stream.BaseStream.Seek(Position, SeekOrigin.Begin);
                        }
                    }
                    b = _stream.ReadByte();
                    Position += 1;
                }
                return false;
            }
            catch (EndOfStreamException ex)
            {
                Console.WriteLine("Attempted to read past end of stream.");
                ReadErrors.Add("Attempted to read past end of stream.");
                eventHeader = null;
                return false;
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"Error reading GT3X data: {ex2.Message}");
                ReadErrors.Add($"Error reading GT3X data: {ex2.Message}");
                eventHeader = null;
                return false;
            }
        }

        public bool TryReadNextEvent(out Gt3xRawEventModel outputRawEvent)
        {
            try
            {
                Gt3xRawEventHeader nextValidRawEventHeader = null;
                outputRawEvent = null;

                if (TrySeekToNextValiddRecordHeader(out nextValidRawEventHeader))
                {
                    if (nextValidRawEventHeader == null)
                    {
                        string text = $"Invalid GT3X event at position: {Position}.";
                        ReadErrors.Add(text);
                        throw new Gt3xFormatException(text);
                    }
                }

                byte b = _stream.ReadByte();
                Position += 1;

                if (b != Gt3xRawEventModel.LogSeparatorValue)
                {
                    Console.WriteLine($"Bad separator value at {Position}. Expected value: {Gt3xRawEventModel.LogSeparatorValue} but read value: {b}.");
                    ReadErrors.Add($"Bad separator value at {Position}. Expected value: {Gt3xRawEventModel.LogSeparatorValue} but read value: {b}.");
                    nextValidRawEventHeader = null;
                    outputRawEvent = null;
                    return false;
                }
                Gt3xRawEventModel rawEvent = new Gt3xRawEventModel
                {
                    Separator = b,
                    LogRecordHeader = new Gt3xRawEventHeader()
                    {
                        EventType = _stream.ReadByte(),
                        Timestamp = _stream.ReadUInt32(),
                        Size = _stream.ReadUInt16()
                    }
                };
                rawEvent.Payload = _stream.ReadBytes(rawEvent.LogRecordHeader.Size);
                rawEvent.Checksum = _stream.ReadByte();

                if (!rawEvent.ChecksumIsValid)
                {
                    _stream.BaseStream.Seek(Position, SeekOrigin.Begin); // set back to next byte

                    Console.WriteLine($"Invalid GT3X event checksum at position: {Position}. Expected checksum {rawEvent.CalculateChecksum()}, got checksum {rawEvent.Checksum}.");
                    ReadErrors.Add($"Invalid GT3X event checksum at position: {Position}. Expected checksum {rawEvent.CalculateChecksum()}, got checksum {rawEvent.Checksum}.");
                    outputRawEvent = null;
                    return false;
                }

                Position += 8 + rawEvent.LogRecordHeader.Size;
                outputRawEvent = rawEvent;
                return rawEvent.ChecksumIsValid;
            }
            catch (EndOfStreamException)
            {
                Console.WriteLine("Attempted to read past end of stream.");
                ReadErrors.Add("Attempted to read past end of stream.");
                outputRawEvent = null;
                throw;
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"Error reading GT3X data: {ex2.Message}");
                ReadErrors.Add($"Error reading GT3X data: {ex2.Message}");
                outputRawEvent = null;
                return false;
            }
        }

        public Gt3xRawEventModel ReadNextEvent()
        {
            if (!TryReadNextEvent(out Gt3xRawEventModel rawEvent))
            {
                if (rawEvent == null)
                {
                    string text = $"Invalid GT3X event at position: {Position}.";
                    ReadErrors.Add(text);
                    throw new Gt3xFormatException(text);
                }
                if (!rawEvent.ChecksumIsValid)
                {
                    string text2 = $"Invalid GT3X event checksum at position: {Position}. Expected checksum {rawEvent.CalculateChecksum()}, got checksum {rawEvent.Checksum}.";
                    ReadErrors.Add(text2);
                    throw new Gt3xFormatException(text2);
                }
            }
            return rawEvent;
        }

        public void Dispose()
        {
            _stream?.Dispose();
        }
    }

    public class LogBinFileReader : IDisposable
    {
        private readonly Stream _stream;

        private Stream _logBinStream;

        public Gt3xStreamReader _gt3xReader;

        public long _logBinLength;

        private readonly bool _manageStream;

        public IReadOnlyList<string> ReadErrors => _gt3xReader?.ReadErrors;

        public LogBinFileReader(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            _stream = stream;
            _manageStream = false;
            Open();
        }

        public LogBinFileReader(string fileName)
        {
            _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            _manageStream = true;
            Open();
        }

        private void Open()
        {
            try
            {
                _logBinLength = _stream.Length;
                _logBinStream = _stream;
                _gt3xReader = new Gt3xStreamReader(_logBinStream);
            }
            catch (Exception innerException)
            {
                throw new Gt3xFormatException("There was an error opening log.bin", innerException);
            }
        }

        public Gt3xRawEventModel ReadEvent()
        {
            if (_gt3xReader.Position >= _logBinLength)
            {
                return null;
            }
            return _gt3xReader.ReadNextEvent();
        }

        public bool TryReadEvent(out Gt3xRawEventModel rawEvent)
        {
            if (_gt3xReader.Position >= _logBinLength)
            {
                rawEvent = null;
                return false;
            }
            return _gt3xReader.TryReadNextEvent(out rawEvent);
        }



        public bool TryReadAllEvents(out IList<Gt3xRawEventModel> rawEvents)
        {
            rawEvents = new List<Gt3xRawEventModel>();
            while (_gt3xReader.Position < _logBinLength)
            {
                if (!TryReadEvent(out Gt3xRawEventModel rawEvent))
                {
                    return false;
                }
                rawEvents.Add(rawEvent);
            }
            return true;
        }

        public IEnumerable<Gt3xRawEventModel> ReadAllEvents()
        {
            if (!TryReadAllEvents(out IList<Gt3xRawEventModel> rawEvents))
            {
                foreach (string readError in ReadErrors)
                {
                    Console.WriteLine($"ERROR: {readError}");
                }
                throw new Gt3xFormatException($"Invalid GT3X Event at position {_gt3xReader.Position}");
            }
            return rawEvents;
        }

        public void Dispose()
        {
            _gt3xReader?.Dispose();
            _logBinStream?.Dispose();

            if (_manageStream)
            {
                _stream.Dispose();
            }
        }
    }

    public class Gt3xFormatException : Exception
    {
        public Gt3xFormatException() { }

        public Gt3xFormatException(string message)
            : base(message) { }

        public Gt3xFormatException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}