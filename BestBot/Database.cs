
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System;
using System.Net;
using Microsoft.Azure.Cosmos;


namespace BestBot
{
    public static class CosmosBestBot
    {
        
        private static readonly string EndpointUri = "https://bestbotdb.documents.azure.com:443/";

        // The primary key for the Azure Cosmos account.
        private static readonly string PrimaryKey = "5QF5cZsYRftcFwRiGaENVhyaXn68k7vthzntXCX1qOASP4igWwOEomR6YeHlqevROw2PMzAMPUFvEWwju8TjTg==";

        // The Cosmos client instance
        private static CosmosClient cosmosClient;

        // The database we will create
        private static Database database;

        // The container we will create.
        private static Container container;

        // The name of the database and container we will create
        private static readonly string databaseId = "BestBotDB";
        private static readonly string containerId = "CitasUGR";

        public static async void Initialize(){
            cosmosClient = new CosmosClient(EndpointUri, 
            PrimaryKey, 
            new CosmosClientOptions()
            {
                ApplicationRegion = Regions.WestEurope,
            });

            await CreateDatabaseAsync();
            await CreateContainerAsync();
            //await AddItemsToContainerAsync();
            Console.WriteLine("Todo conectadito y en marcha: {0}\n");

        }

        private static async Task CreateDatabaseAsync()
        {
            // Create a new database
            database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            Console.WriteLine("Created Database: {0}\n", databaseId);
        }

        private static async Task CreateContainerAsync()
        {
            // Create a new container
            container = await database.CreateContainerIfNotExistsAsync(containerId, "/id");
            Console.WriteLine("Created Container: {0}\n", containerId);
        }


        /*
        Añadir un elemento al contenedor

        private static async Task AddItemsToContainerAsync()
        {
            // Create a family object for the Andersen family
            CitasUGR salud = new CitasUGR
            {
                Id = "granadainformatica",
                Ciudaddb = "GRANADA",
                Facultaddb = "INFORMATICA",

                Citasdb = new Dbcitas[]
                {
                    new Dbcitas { Horadb = "09:00", Correodb="LIBRE" },
                    new Dbcitas { Horadb = "09:30", Correodb="LIBRE" },
                    new Dbcitas { Horadb = "10:00", Correodb="LIBRE" }
                }
            };

            try
            {
                // Read the item to see if it exists.  
                ItemResponse<CitasUGR> andersenFamilyResponse = await container.ReadItemAsync<CitasUGR>(salud.Id, new PartitionKey(salud.Id));
                Console.WriteLine("Item in database with id: {0} already exists\n", andersenFamilyResponse.Resource.Id);
            }
            catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                ItemResponse<CitasUGR> andersenFamilyResponse = await container.CreateItemAsync<CitasUGR>(salud, new PartitionKey(salud.Id));

                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                Console.WriteLine("Created item in database\n");
            }
        }
        */

        /// <summary>
        /// Run a query (using Azure Cosmos DB SQL syntax) against the container
        /// </summary>
        public static async Task<CitasUGR> QueryItemsAsync(string id)
        {
            // Se define la consulta que se hara a la base de datos
            // SELECT * FROM c -> Coge TODO lo que hay en la tabla c 
            // WHERE variable = "valor"; -> filtrar lo que se retorna
            
            // Selecciona todo lo que haya en el item id solo de las tablas que tengan por nombre CIUDAD=ciudad
            // var sqlQueryText = "SELECT * FROM " + id +" WHERE CIUDAD " +" = " + ciudad;
            QueryDefinition query = new QueryDefinition("SELECT * FROM CitasUGR f WHERE f.id = @id")
                .WithParameter("@id", id);
                

            List<CitasUGR> citas = new List<CitasUGR>();
            FeedIterator<CitasUGR> queryResultSetIterator = container.GetItemQueryIterator<CitasUGR>(
                query,
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey(id)
                });          

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<CitasUGR> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (CitasUGR cu in currentResultSet)
                {
                    citas.Add(cu);
                }
            }

            return citas[0];
            
        }
 

        /// <summary>
        /// Replace an item in the container
        /// </summary>
        /*       
            Recogemos un objeto y se serializa a tipos CitasUGR y la respuesta la guarda en itemBody
            y a itemBody le modificamos el campo que se va a actualizar y mediante ReplaceItemAsync 
            cambias el objeto antiguo por uno nuevo con el valor cambiado
        */
       
        public static async Task UpdateItemAsync(string id, string correo, string hora_correcta )
        {
            ItemResponse<CitasUGR> updatacion = await container.ReadItemAsync<CitasUGR>(id, new PartitionKey(id));
            var itemBody = updatacion.Resource;

            if(hora_correcta == "LIBRE"){
                // CANCELAR UNA CITA
                correo = correo.ToUpper();
                foreach(var cor in itemBody.Citasdb){
                    
                    if(cor.Correodb.Equals(correo)){
                        cor.Correodb = hora_correcta; //Se le asigna el valor "LIBRE"
                    }
                }

            }else{
                int valor = itemBody.Citasdb[0].Horadb.IndexOf(hora_correcta);

                foreach(var hor in itemBody.Citasdb){
                    if(hor.Horadb.Equals(hora_correcta)){
                        hor.Correodb = correo;
                    }
                }
            }

            // replace the item with the updated content
            updatacion = await container.ReplaceItemAsync<CitasUGR>(itemBody, itemBody.Id, new PartitionKey(id));
        }

        /// <summary>
        /// Consults items
        /// </summary>
        public static async Task<string> GetItemAsync(string id, string correo){
            ItemResponse<CitasUGR> updatacion = await container.ReadItemAsync<CitasUGR>(id, new PartitionKey(id));
            var itemBody = updatacion.Resource;
            bool existe = false;
            string respuesta = "";
            string hora = "";

            foreach(var cor in itemBody.Citasdb){
                    
                if(cor.Correodb.Equals(correo)){
                    existe = true;
                    hora = cor.Horadb;
                }
            }

            if(existe){
                respuesta = $"Tiene cita a las {hora}";

            }else{
                respuesta = "No tiene ninguna cita reservada ";
            }

            return respuesta;
        }
        

    }
}