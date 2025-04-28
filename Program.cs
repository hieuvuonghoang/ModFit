using System;
using Dynastream.Fit;
using System.IO;
using System.Linq;
using Extensions;
using System.Collections.Generic;
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper;
using System.Data;
using Newtonsoft.Json;
using System.Diagnostics;

namespace ModFit
{
    internal class Program
    {
        // Latitude: 21.06554 degrees is converted to 251321681 semicircles
        static void Main(string[] args)
        {

            var pathFile = "START_28042025180556-SPEED_7-GPX_01.json";
            var jogMockRecords = ReadJogMockRecords(pathFile);

            var startTimeGmtPlus7 = new System.DateTime(2025, 04, 28, 04, 30, 31);
            var gmtPlus7 = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(startTimeGmtPlus7, gmtPlus7);

            var records = new List<Record>();
            var firstTimeInJogMockRecord = jogMockRecords.FirstOrDefault().timestamp;
            var random = new Random();
            byte[] randomByte = new byte[1];
            foreach (var jogMockRecord in jogMockRecords)
            {

                // Convert to semicircles
                var latitudeSemicircles = DegreesToSemicircles(jogMockRecord.latitude);
                var longitudeSemicircles = DegreesToSemicircles(jogMockRecord.longitude);
                // 
                var secondsDouble = jogMockRecord.timestamp.Subtract(jogMockRecords.FirstOrDefault().timestamp).TotalSeconds;
                var secondsInt = (int)secondsDouble;
                //
                var timeStamp = new Dynastream.Fit.DateTime(startTimeUtc.AddSeconds(secondsDouble)).GetTimeStamp();
                //
                var heartRate = (byte)random.Next(115, 115);
                var cadence = (byte)random.Next(60, 60);
                var record = new Record()
                {
                    Lat = latitudeSemicircles,
                    Lng = longitudeSemicircles,
                    Distance = jogMockRecord.distance * 1000,
                    Seconds = secondsInt,
                    Timestamp = timeStamp,
                    HeartRate = heartRate,
                    Cadence = cadence,
                    EnhancedSpeed = jogMockRecord.speed,
                    Speed = jogMockRecord.speed,
                    Altitude = jogMockRecord.altitude,
                    EnhancedAltitude = jogMockRecord.altitude,
                    FractionalCadence = 0
                };
                records.Add(record);
            }

            var activity = new Activity()
            {
                Records = records
            };

            ushort manufacturerId = Manufacturer.Garmin;
            ushort productId = 4033;
            uint serialNumber = 3494402588; // Serial number of the device that created the file

            CreateActivityFile(activity, manufacturerId, productId, serialNumber);

            #region 
            //Console.WriteLine("FIT Decode Example Application");

            ////if (args.Length != 2)
            ////{
            ////    Console.WriteLine("Usage: decode.exe <filename> <starttime(yyyyMMdd HH:mm:ss)>");
            ////    return;
            ////}

            //if (args.Length != 3)
            //{
            //    args = new string[3];
            //    //14262149589_ACTIVITY.fit
            //    //args[0] = "2025-04-13-05-19-48.fit";
            //    //args[0] = "14262149589_ACTIVITY.fit";
            //    args[0] = "2";
            //    args[1] = "activity.json";
            //    args[2] = "20250412 22:19:48";
            //}

            //var type = args[0]; // 1 .fit, 2 .json
            //var input = args[1];
            //var dateTime = System.DateTime.ParseExact(args[2], "yyyyMMdd HH:mm:ss", CultureInfo.InvariantCulture);

            //ushort manufacturerId = Manufacturer.Garmin;
            //ushort productId = 4033;
            //uint serialNumber = 3494402588; // Serial number of the device that created the file

            //try
            //{
            //    Activity activity = null;

            //    if (type == "1")
            //    {
            //        // Decode the FIT file
            //        activity = Decode(input);
            //    }
            //    else if (type == "2")
            //    {
            //        // Read the JSON file
            //        var jsonString = System.IO.File.ReadAllText(input);
            //        activity.Records = JsonConvert.DeserializeObject<List<Record>>(jsonString);
            //    }

            //    if (activity == null)
            //    {
            //        Console.WriteLine("Failed to decode the FIT file.");
            //        return;
            //    }
            //    // Define the GMT+7 time zone
            //    var gmtPlus7 = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            //    // Convert the DateTime to UTC
            //    var dateTimeUtc = TimeZoneInfo.ConvertTimeToUtc(dateTime, gmtPlus7);
            //    //
            //    var dateTimeStart = new Dynastream.Fit.DateTime(dateTimeUtc);
            //    // 
            //    var timeDifference = dateTimeStart.GetTimeStamp() - activity.StartTime.GetTimeStamp();
            //    // 
            //    var pathFit = CreateActivityFile(activity, dateTimeStart, timeDifference, manufacturerId, productId, serialNumber);

            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"Exception {ex}");
            //}
            #endregion
        }


        // Constants for conversion
        private static double SEMICIRCLES_PER_DEGREE = (double)(Math.Pow(2, 31)) / 180.0;

        // Method to convert degrees to semicircles
        public static int DegreesToSemicircles(double degrees)
        {
            return (int)(degrees * SEMICIRCLES_PER_DEGREE);
        }

        // Method to convert semicircles to degrees
        public static double SemicirclesToDegrees(int semicircles)
        {
            return semicircles / SEMICIRCLES_PER_DEGREE;
        }


        public static List<JogMockRecord> ReadJogMockRecords(string pathFile)
        {
            var jsonString = System.IO.File.ReadAllText(pathFile);
            var ret = JsonConvert.DeserializeObject<List<JogMockRecord>>(jsonString);
            return ret;
        }

        static DataTable ReadCsvToDataTable(string filePath)
        {
            var dataTable = new DataTable();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                //HasHeaderRecord = true, // Cấu hình để có hàng tiêu đề
                //HeaderValidated = null, // Bỏ qua việc xác thực tiêu đề
                MissingFieldFound = null // Bỏ qua việc tìm trường thiếu
            };
            using (var reader = new StreamReader(filePath))
            {
                using (var csv = new CsvReader(reader, config))
                {
                    // Bỏ qua hàng đầu tiên
                    csv.Read();
                    using (var dr = new CsvDataReader(csv))
                    {
                        dataTable.Load(dr);
                    }
                }
            }
            return dataTable;
        }

        public static Activity Decode(string pathFile)
        {
            // Attempt to open the input file
            FileStream fileStream = new FileStream(pathFile, FileMode.Open);
            Console.WriteLine($"Opening {pathFile}");

            // Create our FIT Decoder
            FitDecoder fitDecoder = new FitDecoder(fileStream, Dynastream.Fit.File.Activity);

            // Decode the FIT file
            try
            {
                Console.WriteLine("Decoding...");
                fitDecoder.Decode();
            }
            catch (FileTypeException ex)
            {
                Console.WriteLine("DecodeDemo caught FileTypeException: " + ex.Message);
                return null;
            }
            catch (FitException ex)
            {
                Console.WriteLine("DecodeDemo caught FitException: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("DecodeDemo caught Exception: " + ex.Message);
            }
            finally
            {
                fileStream.Close();
            }

            // Check the time zone offset in the Activity message.
            var timezoneOffset = fitDecoder.FitMessages.ActivityMesgs.FirstOrDefault()?.TimezoneOffset();
            Console.WriteLine($"The timezone offset for this activity file is {timezoneOffset?.TotalHours ?? 0} hours.");

            // Create the Activity Parser and group the messages into individual sessions.
            ActivityParser activityParser = new ActivityParser(fitDecoder.FitMessages);
            var sessions = activityParser.ParseSessions();

            // Export a CSV file for each Activity Session
            Activity activity = new Activity();
            foreach (SessionMessages session in sessions)
            {
                if (session.Records.Count > 0)
                {
                    var recordsCSV = Export.RecordsToCSV(session, fitDecoder.RecordFieldNames, fitDecoder.RecordDeveloperFieldNames);
                    var recordsPath = Path.Combine(Path.GetDirectoryName(pathFile), $"{Path.GetFileNameWithoutExtension(pathFile)}_{session.Session.GetStartTime().GetDateTime().ToString("yyyyMMddHHmmss")}_{session.Session.GetSport()}_Records.csv");
                    using (StreamWriter outputFile = new StreamWriter(recordsPath))
                    {
                        outputFile.WriteLine(recordsCSV);
                    }

                    var dataTable = ReadCsvToDataTable(recordsPath);
                    var records = ConvertDataTableToRecords(dataTable);
                    activity.Records = records;
                }
            }
            return activity;
        }

        static public List<Mesg> CreateTimeBasedActivity(Activity activity, Dynastream.Fit.DateTime startTime)
        {
            var messages = new List<Mesg>();

            // Timer Events are a BEST PRACTICE for FIT ACTIVITY files
            var eventMesgStart = new EventMesg();
            eventMesgStart.SetTimestamp(startTime);
            eventMesgStart.SetEvent(Event.Timer);
            eventMesgStart.SetEventType(EventType.Start);
            messages.Add(eventMesgStart);

            // Create the Developer Id message for the developer data fields.
            var developerIdMesg = new DeveloperDataIdMesg();
            // It is a BEST PRACTICE to reuse the same Guid for all FIT files created by your platform
            byte[] appId = new Guid("00010203-0405-0607-0809-0A0B0C0D0E0F").ToByteArray();
            for (int i = 0; i < appId.Length; i++)
            {
                developerIdMesg.SetApplicationId(i, appId[i]);
            }
            developerIdMesg.SetDeveloperDataIndex(0);
            developerIdMesg.SetApplicationVersion(110);
            messages.Add(developerIdMesg);

            // Create the Developer Data Field Descriptions
            var doughnutsFieldDescMesg = new FieldDescriptionMesg();
            doughnutsFieldDescMesg.SetDeveloperDataIndex(0);
            doughnutsFieldDescMesg.SetFieldDefinitionNumber(0);
            doughnutsFieldDescMesg.SetFitBaseTypeId(FitBaseType.Float32);
            doughnutsFieldDescMesg.SetFieldName(0, "Doughnuts Earned");
            doughnutsFieldDescMesg.SetUnits(0, "doughnuts");
            doughnutsFieldDescMesg.SetNativeMesgNum(MesgNum.Session);
            messages.Add(doughnutsFieldDescMesg);

            FieldDescriptionMesg hrFieldDescMesg = new FieldDescriptionMesg();
            hrFieldDescMesg.SetDeveloperDataIndex(0);
            hrFieldDescMesg.SetFieldDefinitionNumber(1);
            hrFieldDescMesg.SetFitBaseTypeId(FitBaseType.Uint8);
            hrFieldDescMesg.SetFieldName(0, "Heart Rate");
            hrFieldDescMesg.SetUnits(0, "bpm");
            hrFieldDescMesg.SetNativeFieldNum(RecordMesg.FieldDefNum.HeartRate);
            hrFieldDescMesg.SetNativeMesgNum(MesgNum.Record);
            messages.Add(hrFieldDescMesg);

            // Every FIT ACTIVITY file MUST contain Record messages
            //var timestamp = new Dynastream.Fit.DateTime(startTime);
            //uint timestamp = startTime;

            // Add record
            Dynastream.Fit.DateTime dateTime = null;
            foreach (var record in activity.Records)
            {
                var newRecord = new RecordMesg();
                dateTime = new Dynastream.Fit.DateTime(record.Timestamp);
                newRecord.SetTimestamp(dateTime);
                newRecord.SetPositionLat(record.Lat);
                newRecord.SetPositionLong(record.Lng);
                newRecord.SetDistance(record.Distance);
                newRecord.SetAltitude(record.Altitude);
                newRecord.SetSpeed(record.Speed);
                newRecord.SetHeartRate(record.HeartRate);
                newRecord.SetCadence(record.Cadence);
                newRecord.SetFractionalCadence(record.FractionalCadence);
                newRecord.SetEnhancedAltitude(record.EnhancedAltitude);
                newRecord.SetEnhancedSpeed(record.EnhancedSpeed);
                messages.Add(newRecord);
            }

            // Timer Events are a BEST PRACTICE for FIT ACTIVITY files
            var eventMesgStop = new EventMesg();
            eventMesgStop.SetTimestamp(new Dynastream.Fit.DateTime(dateTime));
            eventMesgStop.SetEvent(Event.Timer);
            eventMesgStop.SetEventType(EventType.StopAll);
            messages.Add(eventMesgStop);

            // Every FIT ACTIVITY file MUST contain at least one Lap message
            var lapMesg = new LapMesg();
            lapMesg.SetMessageIndex(0);
            lapMesg.SetTimestamp(dateTime);
            lapMesg.SetStartTime(startTime);
            lapMesg.SetTotalElapsedTime(dateTime.GetTimeStamp() - startTime.GetTimeStamp());
            lapMesg.SetTotalTimerTime(dateTime.GetTimeStamp() - startTime.GetTimeStamp());
            messages.Add(lapMesg);

            // Every FIT ACTIVITY file MUST contain at least one Session message
            var sessionMesg = new SessionMesg();
            sessionMesg.SetMessageIndex(0);
            sessionMesg.SetTimestamp(dateTime);
            sessionMesg.SetStartTime(startTime);
            sessionMesg.SetTotalElapsedTime(dateTime.GetTimeStamp() - startTime.GetTimeStamp());
            sessionMesg.SetTotalTimerTime(dateTime.GetTimeStamp() - startTime.GetTimeStamp());
            sessionMesg.SetSport(Sport.Running);
            sessionMesg.SetSubSport(SubSport.Generic);
            sessionMesg.SetFirstLapIndex(0);
            sessionMesg.SetNumLaps(1);
            //sessionMesg.SetTotalCalories(915);

            // Add a Developer Field to the Session message
            var doughnutsEarnedDevField = new DeveloperField(doughnutsFieldDescMesg, developerIdMesg);
            doughnutsEarnedDevField.SetValue(sessionMesg.GetTotalElapsedTime() / 1200.0f);
            sessionMesg.SetDeveloperField(doughnutsEarnedDevField);
            messages.Add(sessionMesg);

            // Every FIT ACTIVITY file MUST contain EXACTLY one Activity message
            var activityMesg = new ActivityMesg();
            activityMesg.SetTimestamp(dateTime);
            activityMesg.SetNumSessions(1);
            var timezoneOffset = (int)TimeZoneInfo.Local.BaseUtcOffset.TotalSeconds;
            activityMesg.SetLocalTimestamp((uint)((int)dateTime.GetTimeStamp() + timezoneOffset));
            activityMesg.SetTotalTimerTime(dateTime.GetTimeStamp() - new Dynastream.Fit.DateTime(startTime).GetTimeStamp());
            messages.Add(activityMesg);

            return messages;
        }

        public static void CreateActivityFile(Activity activity, ushort manufacturerId, ushort productId, uint serialNumber)
        {
            var fileName = $"{Guid.NewGuid()}.fit";

            var startTime = activity.StartTime;

            var messages = CreateTimeBasedActivity(activity, startTime);
            // The combination of file type, manufacturer id, product id, and serial number should be unique.
            // When available, a non-random serial number should be used.
            Dynastream.Fit.File fileType = Dynastream.Fit.File.Activity;

            float softwareVersion = 1.0f;

            // Every FIT file MUST contain a File ID message
            var fileIdMesg = new FileIdMesg();
            fileIdMesg.SetType(fileType);
            fileIdMesg.SetManufacturer(manufacturerId);
            fileIdMesg.SetProduct(productId);
            fileIdMesg.SetTimeCreated(startTime);
            fileIdMesg.SetSerialNumber(serialNumber);

            // A Device Info message is a BEST PRACTICE for FIT ACTIVITY files
            var deviceInfoMesg = new DeviceInfoMesg();
            deviceInfoMesg.SetDeviceIndex(DeviceIndex.Creator);
            deviceInfoMesg.SetManufacturer(manufacturerId);
            deviceInfoMesg.SetProduct(productId);
            deviceInfoMesg.SetSerialNumber(serialNumber);
            deviceInfoMesg.SetSoftwareVersion(softwareVersion);
            deviceInfoMesg.SetTimestamp(startTime);

            var myUserProfile = new UserProfileMesg();
            myUserProfile.SetGender(Gender.Male);
            float myWeight = 63.1F;
            myUserProfile.SetWeight(myWeight);

            // Create the output stream, this can be any type of stream, including a file or memory stream. Must have read/write access
            var fitDest = new FileStream(path: fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

            // Create a FIT Encode object
            var encoder = new Encode(ProtocolVersion.V20);

            // Write the FIT header to the output stream
            encoder.Open(fitDest);

            // Write the messages to the file, in the proper sequence
            encoder.Write(fileIdMesg);
            encoder.Write(deviceInfoMesg);
            encoder.Write(myUserProfile);

            foreach (Mesg message in messages)
            {
                encoder.Write(message);
            }

            // Update the data size in the header and calculate the CRC
            encoder.Close();

            // Close the output stream
            fitDest.Close();

            Console.WriteLine($"Encoded FIT file {fitDest.Name}");
        }

        public static List<Record> ConvertDataTableToRecords(DataTable dataTable)
        {
            var records = new List<Record>();

            foreach (DataRow row in dataTable.Rows)
            {
                var record = new Record
                {
                    Seconds = Convert.ToInt32(row["Seconds"]),
                    Timestamp = Convert.ToUInt32(row["Timestamp"]),
                    Lat = Convert.ToInt32(row["PositionLat"]),
                    Lng = Convert.ToInt32(row["PositionLong"]),
                    Distance = Convert.ToSingle(row["Distance"]),
                    HeartRate = Convert.ToByte(row["HeartRate"]),
                    Cadence = Convert.ToByte(row["Cadence"]),
                    FractionalCadence = Convert.ToSingle(row["FractionalCadence"]),
                    EnhancedAltitude = Convert.ToSingle(row["EnhancedAltitude"]),
                    EnhancedSpeed = Convert.ToSingle(row["EnhancedSpeed"])
                };

                records.Add(record);
            }

            return records;
        }

        public static Activity ReadCsvToActivity(string csvString)
        {
            var activity = new Activity();
            var lines = csvString.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            //#Records,Running,Generic,2025-04-12 22:19:48,10016.28,936,4027.166
            var activityStr = lines[0].Split(',');
            //activity.Distance = double.Parse(activityStr[4], CultureInfo.InvariantCulture);
            //activity.Calories = double.Parse(activityStr[5], CultureInfo.InvariantCulture);
            activity.Records = ReadCsvToRecords(lines);
            return activity;
        }

        public static List<Record> ReadCsvToRecords(string[] lines)
        {
            var records = new List<Record>();
            // Seconds,Timestamp,PositionLat,PositionLong,Distance,Altitude,Speed,HeartRate,Cadence,FractionalCadence,EnhancedAltitude,EnhancedSpeed,TimerEvent,Lap
            var headers = lines[1].Split(',');

            for (int i = 2; i < lines.Length; i++)
            {
                lines[i] = lines[i].Replace("\r", "");
                var values = lines[i].Split(',');
                var record = new Record
                {
                    Seconds = int.Parse(values[0]),
                    Timestamp = uint.Parse(values[1]),
                    Lat = int.Parse(values[2], CultureInfo.InvariantCulture),
                    Lng = int.Parse(values[3], CultureInfo.InvariantCulture),
                    Distance = float.Parse(values[4], CultureInfo.InvariantCulture),
                    Altitude = float.Parse(values[5], CultureInfo.InvariantCulture),
                    Speed = float.Parse(values[6], CultureInfo.InvariantCulture),
                    HeartRate = byte.Parse(values[7]),
                    Cadence = byte.Parse(values[8]),
                    FractionalCadence = float.Parse(values[9], CultureInfo.InvariantCulture),
                    EnhancedAltitude = float.Parse(values[10], CultureInfo.InvariantCulture),
                    EnhancedSpeed = float.Parse(values[11], CultureInfo.InvariantCulture)
                };
                records.Add(record);
            }
            return records;
        }

    }

    public class Activity
    {
        public Dynastream.Fit.DateTime StartTime
        {
            get
            {
                if (Records == null || Records.Count == 0)
                    throw new ArgumentNullException(nameof(Records), "Records cannot be null or empty.");
                return new Dynastream.Fit.DateTime(Records!.FirstOrDefault()!.Timestamp!);
            }
        }
        public List<Record> Records { get; set; }
    }

    public class Record
    {
        public int Seconds { get; set; }
        public uint Timestamp { get; set; }
        public int Lat { get; set; }
        public int Lng { get; set; }
        public float Distance { get; set; }
        public float Altitude { get; set; }
        public float Speed { get; set; }
        public byte HeartRate { get; set; }
        public byte Cadence { get; set; }
        public float FractionalCadence { get; set; }
        public float EnhancedAltitude { get; set; }
        public float EnhancedSpeed { get; set; }
    }

    public class JogMockRecord
    {
        public float latitude { get; set; }
        public float longitude { get; set; }
        public float altitude { get; set; }
        public System.DateTime timestamp { get; set; }
        public float speed { get; set; }
        public float distance { get; set; }
    }

}

