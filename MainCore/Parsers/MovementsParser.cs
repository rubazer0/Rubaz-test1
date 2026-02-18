using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MainCore.Parsers
{
    public static class MovementsParser
    {
        public static List<TimeSpan> GetIncomingAttacks(HtmlDocument doc)
        {
            var table = doc.GetElementbyId("movements");
            if (table is null) return new List<TimeSpan>();

            var rows = table.Descendants("tr");
            var attacks = new List<TimeSpan>();
            bool isIncoming = false;

            foreach (var row in rows)
            {
                var th = row.Descendants("th").FirstOrDefault();
                if (th != null)
                {
                    var text = th.InnerText.Trim();
                    if (text.Contains("Incoming troops") || text.Contains("troops")) isIncoming = true;
                    if (text.Contains("Outgoing troops")) isIncoming = false;
                    continue;
                }

                if (!isIncoming) continue;

                var img = row.Descendants("img").FirstOrDefault();
                if (img is null) continue;

                var imgClass = img.GetAttributeValue("class", "");
                if (imgClass.Contains("att"))
                {
                    var timer = row.Descendants("span").FirstOrDefault(x => x.HasClass("timer"));
                    if (timer != null)
                    {
                        var seconds = timer.GetAttributeValue("value", 0);
                        if (seconds > 0) attacks.Add(TimeSpan.FromSeconds(seconds));
                    }
                }
            }
            return attacks;
        }
    }
}
