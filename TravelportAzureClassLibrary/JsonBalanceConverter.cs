using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TravelportAzureClassLibrary
{
    internal class JsonBalanceConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(double);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string balanceString = ((string)reader.Value).Replace("$", "").Replace(",", "");
            return double.TryParse(balanceString, NumberStyles.Any, CultureInfo.InvariantCulture, out double balance) ? balance : 0;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }
    }
}
