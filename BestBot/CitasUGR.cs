using Newtonsoft.Json;

namespace BestBot
{
    public class CitasUGR
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public string Ciudaddb { get; set; }
        public string Facultaddb { get; set; }
        public Dbcitas[] Citasdb { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class Dbcitas
    {
        public string Horadb { get; set; }
        public string Correodb { get; set; }
    }
}