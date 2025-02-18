using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class ArpItem
    {
        public string Ip { get; set; } = string.Empty;

        public string MacAddress { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;
    }

    public static class RaspberryPiDiscoverService
    {
        private static async Task<List<ArpItem>> GetArpResult()
        {
            var (code, output) = await AppService.ExecuteCommandAsync("arp", ["-a"]);

            return ParseArpResult(output);
        }

        private static List<ArpItem> ParseArpResult(string output)
        {
            var lines = output.Split('\n');

            var result = from line in lines
                         let item = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                         where item.Count() == 4
                         select new ArpItem()
                         {
                             Ip = item[0],
                             MacAddress = item[1],
                             Type = item[2]
                         };

            return result.ToList();
        }

        private static bool IsRaspberryPI(string mac)
        {
            string[] macPrefix = ["28-CD-C1", "B8-27-EB", "D8-3A-DD", "DC-A6-32", "E4-5F-01"];

            foreach (var item in macPrefix)
            {
                if (mac.StartsWith(item, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static async Task<string> DetectRaspberryPIIPAddress()
        {
            List<ArpItem> dynamic = await GetArpResult();
            var pis = dynamic.Where(x => IsRaspberryPI(x.MacAddress) == true);

            string ip = pis.FirstOrDefault()?.Ip ?? string.Empty;

            return ip;
        }
    }
}
