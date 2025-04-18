using System;
using Dynastream.Fit;
using System.IO;
using System.Linq;
using Extensions;
using System.Collections.Generic;
using System.Globalization;

namespace ModFit
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("FIT Decode Example Application");

            // Original DateTime in GMT+7
            //var dateTime = new System.DateTime(2025, 04, 17, 19, 03, 00);

            if (args.Length != 2)
            {
                Console.WriteLine("Usage: decode.exe <filename> <starttime(yyyyMMdd HH:mm:ss)>");
                return;
            }

            var dateTime = System.DateTime.ParseExact(args[1], "yyyyMMdd HH:mm:ss", CultureInfo.InvariantCulture);

            ushort manufacturerId = Manufacturer.Garmin;
            ushort productId = 4033;
            uint serialNumber = 3494402588; // Serial number of the device that created the file

            try
            {
                var activity = Decode(args[0]);
                if (activity == null)
                {
                    Console.WriteLine("Failed to decode the FIT file.");
                    return;
                }
                // Define the GMT+7 time zone
                var gmtPlus7 = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                // Convert the DateTime to UTC
                var dateTimeUtc = TimeZoneInfo.ConvertTimeToUtc(dateTime, gmtPlus7);
                //
                var dateTimeStart = new Dynastream.Fit.DateTime(dateTimeUtc);
                // 
                var timeDifference = dateTimeStart.GetTimeStamp() - activity.StartTime.GetTimeStamp();
                // 
                var pathFit = CreateActivityFile(activity, dateTimeStart, timeDifference, manufacturerId, productId, serialNumber);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception {ex}");
            }
        }

        static Activity Decode(string pathFile)
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
            Activity activity = null;
            foreach (SessionMessages session in sessions)
            {
                if (session.Records.Count > 0)
                {
                    var recordsCSV = Export.RecordsToCSV(session, fitDecoder.RecordFieldNames, fitDecoder.RecordDeveloperFieldNames);
                    activity = ReadCsvToActivity(recordsCSV);
                }
            }
            if(activity != null)
            {
                activity.StartTime = fitDecoder.FitMessages.RecordMesgs.FirstOrDefault()?.GetTimestamp();
            }
            return activity;
        }

        static public List<Mesg> CreateTimeBasedActivity(Activity activity, Dynastream.Fit.DateTime startTime, uint timeDifference)
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
                dateTime = new Dynastream.Fit.DateTime(record.Timestamp + timeDifference);
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
            sessionMesg.SetTotalCalories(915);

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
            //CreateActivityFile(messages, FileName, new Dynastream.Fit.DateTime(startTime));

        }

        static string CreateActivityFile(Activity activity, Dynastream.Fit.DateTime startTime, uint timeDifference, ushort manufacturerId, ushort productId, uint serialNumber)
        {
            var fileName = $"{Guid.NewGuid()}.fit";

            var messages = CreateTimeBasedActivity(activity, startTime, timeDifference);
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
            Encode encoder = new Encode(ProtocolVersion.V20);

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

            return fileName;
        }

        static Activity ReadCsvToActivity(string csvString)
        {
            var activity = new Activity();
            var lines = csvString.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            //#Records,Running,Generic,2025-04-12 22:19:48,10016.28,936,4027.166
            var activityStr = lines[0].Split(',');
            activity.Distance = double.Parse(activityStr[4], CultureInfo.InvariantCulture);
            activity.Calories = double.Parse(activityStr[5], CultureInfo.InvariantCulture);
            activity.Records = ReadCsvToRecords(lines);
            return activity;
        }

        static List<Record> ReadCsvToRecords(string[] lines)
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
                    EnhancedSpeed = float.Parse(values[11], CultureInfo.InvariantCulture),
                    TimerEvent = int.Parse(values[12])
                };
                records.Add(record);
            }
            return records;
        }

    }




    public class Activity
    {
        public Dynastream.Fit.DateTime StartTime { get; set; }
        public double Distance { get; set; }
        public double Calories { get; set; }
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
        public float TimerEvent { get; set; }
        public double Lap { get; set; }
    }
}

