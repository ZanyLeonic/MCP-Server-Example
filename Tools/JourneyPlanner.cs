using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace MCPServerDemo.Tools
{
    [McpServerToolType]
    public class JourneyPlanner
    {
        [McpServerTool, Description("Returns the possible A-to-B journeys between two point of interests")]
        public static async Task<string> PlanJourney(HttpClient client,
            [Description("Origin of the journey. Can be expressed as  \"lat,long\", a UK postcode, a Naptan (StopPoint) id, an ICS StopId, or a free-text string (will cause disambiguation unless it exactly matches a point of interest name).")] string fromLocation,
            [Description("Destination of the journey. Can be WGS84 coordinates expressed as \"lat,long\", a UK postcode, a Naptan (StopPoint) id, an ICS StopId, or a free-text string (will cause disambiguation unless it exactly matches a point of interest name).")] string toLocation,
            [Description("(Optional) Travel through point on the journey. Can be WGS84 coordinates expressed as \"lat,long\", a UK postcode, a Naptan (StopPoint) id, an ICS StopId, or a free-text string (will cause disambiguation unless it exactly matches a point of interest name).")] string via = "",
            [Description("(Optional) Does the journey cover stops outside London zones? eg. \"nationalSearch=true\"")] bool nationalSearch = false,
            [Description("(Optional) The date must be in yyyyMMdd format")] string date = "",
            [Description("(Optional) The time must be in HHmm format")] string time = "",
            [Description("(Optional) Does the time given relate to arrival or leaving time? Possible options: \"departing\" | \"arriving\" (Default: departing)")] string timeIs = "departing",
            [Description("(Optional) The journey preference eg possible options: \"leastinterchange\" | \"leasttime\" | \"leastwalking\" (Default: leastinterchange)")] string journeyPreference = "leastinterchange",
            [Description("(Optional) The mode must be a comma separated list of modes. eg possible options (and default): \"bus,overground,national-rail,tube,coach,dlr,cable-car,tram,river-bus,walking,cycle\"")] string mode = "bus,overground,national-rail,tube,coach,dlr,cable-car,tram,river-bus,walking,cycle",
            [Description("(Optional) A boolean to make Journey Planner return alternative routes. Alternative routes are calculated by removing one or more lines included in the fastest route and re-calculating. By default, these journeys will not be returned. (Default: false)")] bool includeAlternativeRoutes = false)
        {
            var url = $"/Journey/JourneyResults/{fromLocation}/to/{toLocation}?nationalSearch={nationalSearch}&timeIs={timeIs}&journeyPreference={journeyPreference}&mode={mode}&includeAlternativeRoutes={includeAlternativeRoutes}" + (via != "" ? $"&via={via}" : "") + (date != "" ? $"&date={date}" : "") + (time != "" ? $"&time={time}" : "");
            using var response = await client.GetAsync(url);

            JsonDocument jDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement jElem = jDoc.RootElement;

            if (response.StatusCode == System.Net.HttpStatusCode.MultipleChoices)
            {
                StringBuilder output = new StringBuilder();

                output.AppendLine("Location ambiguity response; Re-run this tool with your input clarified on which point of interest you are trying to plan to go or from or via.");
                output.AppendLine("If the station specified is not listed or close, try using the nationalSearch parameter for stations outside of London Zones");

                if (jElem.TryGetProperty("fromLocationDisambiguation", out var fromLocationDis))
                {
                    output.AppendLine(ListDisambiguationOptions("From", fromLocationDis));
                }

                if (jElem.TryGetProperty("toLocationDisambiguation", out var toLocationDis))
                {
                    output.AppendLine(ListDisambiguationOptions("To", toLocationDis));
                }

                if (jElem.TryGetProperty("viaLocationDisambiguation", out var viaLocationDis))
                {
                    output.AppendLine(ListDisambiguationOptions("Via", viaLocationDis));
                }

                return output.ToString();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return "No journeys found. You can retry replacing words like \"Railway Station\" or \"Rail Station\" to see if you get better results. Especially for routes outside London.";
            }
            else if (!response.IsSuccessStatusCode)
            {
                return $"Cannot retrieve journeys from API ({response.StatusCode})";
            }

            var journeys = jElem.GetProperty("journeys").EnumerateArray();
            StringBuilder journeySummary = new StringBuilder();

            journeySummary.AppendLine($"Journey(s) from {fromLocation} to {toLocation}");
            int jOption = 1;

            foreach (var journey in journeys)
            {
                journeySummary.AppendLine($"Option {jOption}: ");

                var legs = journey.GetProperty("legs").EnumerateArray();
                int legStep = 1;
                foreach (var leg in legs)
                {
                    journeySummary.AppendLine($"{legStep}. => {leg.GetProperty("instruction").GetProperty("summary").ToString()} (Length: {leg.GetProperty("duration").ToString()} min(s))");
                    journeySummary.AppendLine($"   Departs: {leg.GetProperty("departureTime").GetString()}");
                    journeySummary.AppendLine($"   Arrives: {leg.GetProperty("arrivalTime").GetString()}");

                    if (leg.TryGetProperty("path", out var path))
                    {
                        int pointIdx = 1;
                        var callingPoints = path.GetProperty("stopPoints").EnumerateArray();

                        journeySummary.Append("Calling at: ");

                        foreach (var callingPoint in callingPoints)
                        {
                            string pointFriendly = "Unknown";
                            string pointId = "???";

                            if (callingPoint.TryGetProperty("name", out var nameObj))
                            {
                                pointFriendly = nameObj.ToString();
                            }
                            else if (callingPoint.TryGetProperty("commonName", out var commonNameObj))
                            {
                                pointFriendly = commonNameObj.ToString();
                            }

                            if (callingPoint.TryGetProperty("id", out var idObj))
                            {
                                pointId = idObj.ToString();
                            }

                            if (pointIdx == callingPoints.Count())
                            {
                                journeySummary.Append($"and {pointFriendly} ({pointId}).\n");
                            }
                            else
                            {
                                journeySummary.Append($"{pointFriendly} ({pointId}), ");
                            }
                            pointIdx++;
                        }
                    }

                    if (leg.TryGetProperty("disruptions", out var disruptionObj))
                    {
                        foreach (var disruption in disruptionObj.EnumerateArray())
                        {
                            string desc = "";
                            string closure = "";
                            if (disruption.TryGetProperty("description", out var descObj))
                            {
                                desc = descObj.ToString();
                            }
                            else if (disruption.TryGetProperty("summary", out var sumObj))
                            {
                                desc = sumObj.ToString();
                            }

                            if (disruption.TryGetProperty("closureText", out var closeObj))
                            {
                                closure = closeObj.ToString();
                            }
                            journeySummary.AppendLine($"Disruptions: {desc} {closure}");
                        }
                    }

                    if (leg.TryGetProperty("plannedWorks", out var plannedWorksObj))
                    {
                        foreach (var work in plannedWorksObj.EnumerateArray())
                        {
                            journeySummary.AppendLine($"Planned Engineering Works: {work.GetProperty("description").ToString()}");
                        }
                    }

                    legStep++;
                }
                jOption++;

                if (journey.TryGetProperty("fare", out var fareObj))
                {
                    if (fareObj.TryGetProperty("totalCost", out var totalCostObj) && int.TryParse(totalCostObj.ToString(), out int totalCost))
                    {
                        journeySummary.AppendLine($"Estimated total fare: £{String.Format("{0:0.00}", totalCost / 100)}");
                    }

                    foreach (var fare in fareObj.GetProperty("fares").EnumerateArray())
                    {
                        string fareType = "Unknown";

                        if (fare.TryGetProperty("chargeProfileName", out var profile))
                        {
                            fareType = profile.ToString();
                        }
                        else if (fare.TryGetProperty("type", out var type))
                        {
                            fareType = type.ToString();
                        }

                        if (fare.TryGetProperty("cost", out var costObj) && int.TryParse(costObj.ToString(), out int cost))
                        {
                            journeySummary.AppendLine($"Fare Type: {fareType}, Cost: £{String.Format("{0:0.00}", cost / 100)}");
                        }
                    }

                    if (fareObj.TryGetProperty("caveats", out var caveatObj))
                    {
                        foreach (var caveat in caveatObj.EnumerateArray())
                        {
                            string caveatType = "None";
                            if (caveat.TryGetProperty("type", out var type))
                            {
                                caveatType = type.ToString();
                            }

                            journeySummary.AppendLine($"Caveat: {caveat.GetProperty("text")} ({caveatType})");
                        }
                    }

                }
            }

            journeySummary.AppendLine("The listed information maybe important, please read carefully in detail before travelling.");

            return journeySummary.ToString();
        }

        private static string ListDisambiguationOptions(string label, JsonElement dis)
        {
            StringBuilder sb = new StringBuilder();

            if (!dis.TryGetProperty("disambiguationOptions", out var disambigOpts))
            {
                return "";
            }

            sb.AppendLine($"Options for {label} parameter");

            int i = 1;
            foreach (var option in disambigOpts.EnumerateArray())
            {
                var name = option.GetProperty("place").GetProperty("commonName");
                var param = option.GetProperty("parameterValue");

                sb.AppendLine($"{i}. {name} (parameter: {param})");
                i++;
            }

            return sb.ToString();
        }
    }
}
