using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using System.Collections.Generic;
using passwordless.Models;
using System.Linq;

namespace passwordless
{
    public static class GetUserById
    {
        [FunctionName("GetUserById")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, /*"get",*/ "post",
                Route = null)] IdModel data,
                        [CosmosDB("passwordless", "users",
                ConnectionStringSetting = "CosmosDBConnection",
                SqlQuery = "select * from Users u where u.id = {Id}")]
                IEnumerable<User> users,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger GetUserById function processed a request.");

            var id = data?.Id;

            var user = users.FirstOrDefault();
            if(user != null) Console.WriteLine($"GetUserById for {id}:\r\n{(user == null ? "null" : JsonConvert.SerializeObject(user))}");

            return new OkObjectResult(user);
        }
    }
}
