using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace BatteryDecider
{
    class Program
    {
        private static void ShowUsage()
        {
            Console.WriteLine();
            Console.WriteLine("This program queries the Enphase API for a given solar system and");
            Console.WriteLine("retrieves production and consumption data for a specifed date range.");
            Console.WriteLine("Statistics are then computed including average daily export energy");
            Console.WriteLine("to help in deciding whether adding a battery is warranted (with the");
            Console.WriteLine("assumption being that it's best to self-consume rather than export).");
            Console.WriteLine();
            Console.WriteLine("Usage: BatteryDecider.exe <systemId> <key> <userId> <startDate> <endDate>");
            Console.WriteLine();
            Console.WriteLine("<systemId> - found in the URL for your enphase system e.g. /pv/systems/<systemId>/");
            Console.WriteLine("<key> - developer key, which you can get here https://developer.enphase.com/docs/quickstart.html");
            Console.WriteLine("<userId> - found in your API settings at /pv/settings/<systemId>/");
            Console.WriteLine("<startDate> - YYYY-MM-DD");
            Console.WriteLine("<endDate> - YYYY-MM-DD");
        }

        const long oneDayInSeconds = 60 * 60 * 24;
        const int maxDaysForQueryInterval = 6; // See https://developer.enphase.com/forum/topics/new-7-day-api-limit

        static void Main(string[] args)
        {
            if (args.Length != 5)
            {
                ShowUsage();
                return;
            }

            var systemId = args[0];
            var key = args[1];
            var userId = args[2];
            var startDate = args[3];
            var endDate = args[4];

            var startDateUnix = GetUnixTimestampAtLocalMidnight(startDate);
            var endDateUnix = GetUnixTimestampAtLocalMidnight(endDate);

            var consumption = GetConsumption(startDateUnix, endDateUnix, systemId, key, userId);
            var production = GetProduction(startDateUnix, endDateUnix, systemId, key, userId);

            var dailyConsumptionWh = getDailyWhValues(consumption.intervals, getWhFromConsumptionInterval, startDateUnix);
            var dailyProductionWh = getDailyWhValues(production.intervals, getWhFromProductionInterval, startDateUnix);

            var dailyNetWh = new List<long>();
            for (var i = 0; i < dailyConsumptionWh.Count; i++)
            {
                dailyNetWh.Add(dailyConsumptionWh[i] - dailyProductionWh[i]);
            }

            var averageDailyProductionWh = getAverage(dailyProductionWh);
            var averageDailyConsumptionWh = getAverage(dailyConsumptionWh);
            var averageDailyNetWh = getAverage(dailyNetWh);

            var dailyExportedWh = getDailyExportedWhValues(production.intervals, consumption.intervals, startDateUnix);
            var dailyImportedWh = getDailyImportedWhValues(production.intervals, consumption.intervals, startDateUnix);

            var averageDailyExportedWh = getAverage(dailyExportedWh);
            var averageDailyImportedWh = getAverage(dailyImportedWh);

            Console.WriteLine("==============================================");
            Console.WriteLine("Average daily production (wh):  {0}", averageDailyProductionWh);
            Console.WriteLine("Average daily consumption (wh): {0}", averageDailyConsumptionWh);
            Console.WriteLine("Average daily net energy (wh):  {0}", averageDailyNetWh);
            Console.WriteLine("Average daily exported (wh):    {0}", averageDailyExportedWh);
            Console.WriteLine("Average daily imported (wh):    {0}", averageDailyImportedWh);
            Console.WriteLine("==============================================");
            Console.WriteLine();
            Console.WriteLine("Tip: Although there are other factors to consider, you can compare");
            Console.WriteLine("your average daily imported to average daily exported above to gauge");
            Console.WriteLine("the size of battery you could add to reduce your imports.");
            Console.WriteLine("A battery with a size similar to your average daily exported energy");
            Console.WriteLine("would would allow you to import that much less energy from the grid.");
            Console.WriteLine();
            Console.WriteLine("Press ENTER to continue.");
            Console.ReadLine();
        }

        public static List<long> getDailyImportedWhValues(List<Interval> productionIntervals, List<Interval> consumptionIntervals, long startDateUnix)
        {
            var dailyWhValues = new List<long>();
            var dayIndex = 0;
            var nextdayTimeStamp = startDateUnix + ((dayIndex + 1) * oneDayInSeconds);
            long currentWh = 0;
            for (var i = 0; i < productionIntervals.Count; i++)
            {
                var productionWh = getWhFromProductionInterval(productionIntervals[i]);
                var consumptionWh = getWhFromConsumptionInterval(consumptionIntervals[i]);
                var netWh = productionWh - consumptionWh;
                if (netWh < 0)
                {
                    currentWh += -netWh;
                }
                if (productionIntervals[i].end_at >= nextdayTimeStamp)
                {
                    dayIndex++;
                    nextdayTimeStamp = startDateUnix + ((dayIndex + 1) * oneDayInSeconds);
                    dailyWhValues.Add(currentWh);
                    currentWh = 0;
                }
            }
            return dailyWhValues;
        }

        public static List<long> getDailyExportedWhValues(List<Interval> productionIntervals, List<Interval> consumptionIntervals, long startDateUnix)
        {
            var dailyWhValues = new List<long>();
            var dayIndex = 0;
            var nextdayTimeStamp = startDateUnix + ((dayIndex + 1) * oneDayInSeconds);
            long currentWh = 0;
            for (var i = 0; i < productionIntervals.Count; i++)
            {
                var productionWh = getWhFromProductionInterval(productionIntervals[i]);
                var consumptionWh = getWhFromConsumptionInterval(consumptionIntervals[i]);
                var netWh = productionWh - consumptionWh;
                if (netWh > 0)
                {
                    currentWh += netWh;
                }
                if (productionIntervals[i].end_at >= nextdayTimeStamp)
                {
                    dayIndex++;
                    nextdayTimeStamp = startDateUnix + ((dayIndex + 1) * oneDayInSeconds);
                    dailyWhValues.Add(currentWh);
                    currentWh = 0;
                }
            }
            return dailyWhValues;
        }

        public static double getAverage(List<long> list)
        {
            long total = 0;
            for (var i = 0; i < list.Count; i++)
            {
                total += list[i];
            }
            return Math.Round((double)(total / list.Count));
        }



        private static long getWhFromProductionInterval(Interval interval)
        {
            return interval.wh_del;
        }

        private static long getWhFromConsumptionInterval(Interval interval)
        {
            return interval.enwh;
        }

        private static List<long> getDailyWhValues(List<Interval> intervals, Func<Interval, long> getWhFromIntervalFunction, long startDateUnix)
        {
            var dailyWhValues = new List<long>();
            var dayIndex = 0;
            var nextdayTimeStamp = startDateUnix + ((dayIndex + 1) * oneDayInSeconds);
            long currentWh = 0;
            for (var i = 0; i < intervals.Count; i++)
            {
                currentWh += getWhFromIntervalFunction(intervals[i]);
                if (intervals[i].end_at >= nextdayTimeStamp)
                {
                    dayIndex++;
                    nextdayTimeStamp = startDateUnix + ((dayIndex + 1) * oneDayInSeconds);
                    dailyWhValues.Add(currentWh);
                    currentWh = 0;
                }
            }
            return dailyWhValues;
        }

        private static ProductionResponse GetProduction(long startDateUnix, long endDateUnix, string systemId, string key, string userId)
        {
            var completeResponse = new ProductionResponse() { intervals = new List<Interval>() };
            var web = new WebClient();
            var start = startDateUnix;
            while (true)
            {
                var end = endDateUnix;
                if (endDateUnix - start > maxDaysForQueryInterval * oneDayInSeconds)
                {
                    end = start + (oneDayInSeconds * maxDaysForQueryInterval);
                }
                var url = "https://api.enphaseenergy.com/api/v2/systems/" + systemId + "/rgm_stats?start_at=" + start + "&end_at=" + end + "&key=" + key + "&user_id=" + userId;
                Console.WriteLine(url);
                var production = JsonConvert.DeserializeObject<ProductionResponse>(web.DownloadString(url));
                completeResponse.intervals.AddRange(production.intervals);
                start += (oneDayInSeconds * maxDaysForQueryInterval);
                if (start >= endDateUnix)
                {
                    break;
                }
                Console.WriteLine("{0} days of production to go - next API call will be done shortly.", (endDateUnix - start) / oneDayInSeconds);
                Thread.Sleep(1000 * 30);
            }
            return completeResponse;
        }

        private static ConsumptionResponse GetConsumption(long startDateUnix, long endDateUnix, string systemId, string key, string userId)
        {
            var completeResponse = new ConsumptionResponse() { intervals = new List<Interval>() };
            var web = new WebClient();
            var start = startDateUnix;
            while (true)
            {
                var end = endDateUnix;
                if (endDateUnix - start > maxDaysForQueryInterval * oneDayInSeconds)
                {
                    end = start + (oneDayInSeconds * maxDaysForQueryInterval);
                }
                var url = "https://api.enphaseenergy.com/api/v2/systems/" + systemId + "/consumption_stats?start_at=" + start + "&end_at=" + end + "&key=" + key + "&user_id=" + userId;
                Console.WriteLine(url);
                var consumption = JsonConvert.DeserializeObject<ConsumptionResponse>(web.DownloadString(url));
                completeResponse.intervals.AddRange(consumption.intervals);
                start += (oneDayInSeconds * maxDaysForQueryInterval);
                if (start >= endDateUnix)
                {
                    break;
                }
                Console.WriteLine("{0} days of consumption to go - next API call will be done shortly.", (endDateUnix - start) / oneDayInSeconds);
                Thread.Sleep(1000 * 30);
            }
            return completeResponse;
        }

        private static long GetUnixTimestampAtLocalMidnight(string date)
        {
            var midnight = DateTimeOffset.Parse(date + " 00:00:00");
            return midnight.ToUnixTimeSeconds();
        }
    }

    public class Interval
    {
        public long end_at;
        public long wh_del; // production
        public long enwh; // consumption
    }

    public class ProductionResponse
    {
        public List<Interval> intervals;
    }
    public class ConsumptionResponse
    {
        public List<Interval> intervals;
    }
}
