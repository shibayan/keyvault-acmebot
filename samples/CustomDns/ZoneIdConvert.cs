using System;
using System.Text;

using Azure.Core;

namespace CustomDns;

internal class ZoneIdConvert
{
    public static string ToZoneId(ResourceIdentifier id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(id));

    public static ResourceIdentifier FromZoneId(string zoneId) => new(Encoding.UTF8.GetString(Convert.FromBase64String(zoneId)));
}
