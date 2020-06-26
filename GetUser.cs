using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using System.Linq;
using passwordless.Models;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;

namespace passwordless
{
    public static class GetUser
    {
        [FunctionName("GetUser")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [CosmosDB("passwordless", "users",
                ConnectionStringSetting = "CosmosDBConnection"
                )]
                DocumentClient client,
            ILogger log)
        {
            log.LogInformation("GetUser processed a request.");

            // Try to read the "token" valuef rom the post body, if present
            dynamic data = JsonConvert.DeserializeObject(await new StreamReader(req.Body).ReadToEndAsync());
            var token = data?.token as string;
            Console.WriteLine("GetUser:RequestBody data=\r\n" + (data == null ? "null" : JsonConvert.SerializeObject(data)));

            // Try to fetch from the "ptoken" header, if present
            if(string.IsNullOrEmpty(token)) {
                token = req.Headers.Where(h => h.Key=="ptoken").Select(h => h.Value.FirstOrDefault()).FirstOrDefault();
            }

            // Try to read from the "ptoken" cookie, if present
            if(string.IsNullOrEmpty(token)) {
                token = req.Cookies.Where(c => c.Key == "ptoken")
                    .Select(c => c.Value)
                    .FirstOrDefault();
            }

            // If we still have no token value, return a bad-request result
            if(string.IsNullOrEmpty(token))
                return new BadRequestObjectResult("No token found, either post a JSON body with a \"token\" property, or include a \"ptoken\" header or cookie");
            // Grab the collection URI
            var collectionUri = UriFactory.CreateDocumentCollectionUri("passwordless", "users");
            // Open a document query for the users with the specified token
            var users = client.CreateDocumentQuery<User>(collectionUri, new FeedOptions { EnableCrossPartitionQuery=true })
                .Where(u => u.Token == token)
                .AsDocumentQuery();
            // Grab the first matching user (should by one or zero anyway)
            var user = users.HasMoreResults ? (await users.ExecuteNextAsync()).FirstOrDefault() : null;
            // Log (diagnostics, remove for production)
            Console.WriteLine($"GetUser ({token}):\r\n{(user == null ? "null" : JsonConvert.SerializeObject(user))}");
            // Return the metched user
            return new OkObjectResult(user);
        }
    }
}
