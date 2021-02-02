using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using System.Threading.Tasks;
using System;

namespace BestBot
{
    public class LuisEx
    {

        public enum Intent {
            Saludar,
            CancelarCita,
            Confirmacion,
            PedirCita,
            RecibirCorreo,
            ConsultarCita,
            None
        };
        public Dictionary<Intent, IntentScore> Intents;

        public class _Entities
        {

            // Built-in entities
            //public DateTimeSpec[] datetime;
            // Lists
            public string[][] CiudadList;
            public string[][] FacultadList;
            
            public string[] Correo;

            public CitaClass[] Cita;

            public class CitaClass{
                public FullCitaClass[] FullCita;

                public class FullCitaClass
                {
                    public string[] CitaType;
                    public string[] Ciudad;
                    public string[] Hora;
                    public string[] Facultad;
                    
                }
            }
        }

        public _Entities Entities;

        //Convert LUIS Prediction to Cita orden project
        public static LuisEx Convert(dynamic result)
        {
            LuisEx luisEx = JsonConvert.DeserializeObject<LuisEx>(JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            return luisEx;
        }

        public static int ComprobarDatos(LuisEx luisEx){
            var resultado = 2;
    
            if(luisEx.Entities.Cita != null)
            {
               var la_cita = luisEx.Entities.Cita[0];
               var full_cita = la_cita.FullCita[0];
               string la_ciudad = full_cita.Ciudad[0];
               string la_facultad = full_cita.Facultad[0];

               la_ciudad = la_ciudad.ToLower();
               la_facultad = la_facultad.ToLower();
            

                switch(la_ciudad){
                    case "granada":
                        if(la_facultad.Equals("informatica") || la_facultad.Equals("bellas artes")){
                            resultado = 0;
                        }else{
                            resultado = 1;
                        }
                    break;
                    case "ceuta":
                        if(la_facultad.Equals("arquitectura")){
                            resultado = 0;
                        }else{
                            resultado = 1;
                        }  
                    break;

                    case "melila":
                        if(la_facultad.Equals("salud")){
                            resultado = 0;
                        }else{
                            resultado = 1;
                        }
                            
                    break;
                }

            }
           
            return resultado;
        }


        public static async Task<bool> PedirCita(LuisEx luisEx){
            string id = "";
            var la_cita = luisEx.Entities.Cita[0];
            var full_cita = la_cita.FullCita[0];
            string la_ciudad = full_cita.Ciudad[0];
            string la_facultad = full_cita.Facultad[0];
            string la_hora = full_cita.Hora[0];

            la_ciudad = la_ciudad.ToUpper();
            la_facultad = la_facultad.ToUpper();

            if(la_facultad.Equals("BELLAS ARTES")){
                id = la_ciudad + "ARTES";
            }else{ 
                id = la_ciudad + la_facultad;
            }

            id = id.ToLower();

            CitasUGR cita_ugr = await CosmosBestBot.QueryItemsAsync(id);
            
            //Console.WriteLine(cita_ugr.ToString());

            int hora_correcta = ComprobarHoraDisponible(cita_ugr,la_hora);

            switch(hora_correcta){
               case 0:
                return true;
                case 1:
                return false;
                case -1:
                return false;

            }
 
            return false;
        }

        public static int ComprobarHoraDisponible(CitasUGR cita_ugr, string hora){
            foreach(var hor in cita_ugr.Citasdb){
                if(hor.Horadb.Equals(hora)){
                    if(hor.Correodb.Equals("LIBRE")){
                        // La hora existe y esta libre
                        return 0;
                    }else{
                        // La hora existe pero no esta libre
                        return 1;
                    }
                }
            }
            //Hora incorrecta
            return -1;
        }

        public static async Task RealizarReserva(LuisEx corr, string ciudad_corecta, string facultad_corecta, string hora_correcta){
            string el_correo = corr.Entities.Correo[0];
            el_correo = el_correo.ToUpper();
            // Console.WriteLine(el_correo); -> Funciona

            string id = "";
            ciudad_corecta = ciudad_corecta.ToUpper();
            facultad_corecta = facultad_corecta.ToUpper();

            if(facultad_corecta.Equals("BELLAS ARTES")){
                id = ciudad_corecta + "ARTES";
            }else{ 
                id = ciudad_corecta + facultad_corecta;
            }

            id = id.ToLower();

            //Update de las cita
            await CosmosBestBot.UpdateItemAsync(id,el_correo, hora_correcta);
            Console.WriteLine("Update realizado: {0}\n");
        }


        public (Intent intent, double score) TopIntent()
        {
            Intent maxIntent = Intent.None;
            var max = 0.0;
            foreach (var entry in Intents)
            {
                if (entry.Value.Score > max)
                {
                    maxIntent = entry.Key;
                    max = entry.Value.Score.Value;
                }
            }
            return (maxIntent, max);
        }

        //Helper Function
        private static bool IsNullOrEmpty(Array array)
        {
            return (array == null || array.Length == 0);
        }

       
    }
}
