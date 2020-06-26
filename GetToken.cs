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
using System.Linq;
using passwordless.Models;

namespace passwordless
{
    public static class GetToken
    {
        [FunctionName("GetToken")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", 
                Route = "GetToken/{email}/{shortCode}")] HttpRequest req,
            [CosmosDB("passwordless", "users",
                ConnectionStringSetting = "CosmosDBConnection",
                SqlQuery = "select * from Users u where u.email = {email} and u.shortCode = {shortCode}")]
                IEnumerable<User> users,
            ILogger log)
        {
            log.LogInformation($"GetToken requrested received.");
            var user = users.FirstOrDefault();
            return user == null
                ? (IActionResult)new NotFoundResult()
                : new OkObjectResult(user.Token);
        }
    }
}
