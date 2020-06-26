using System;
using System.Collections.Generic;
//using System.IO;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
//using Newtonsoft.Json;
using passwordless.Models;
//using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using SendGrid.Helpers.Mail;

namespace passwordless
{
    public static class SendTokenEmail
    {
    	private static readonly Random random = new Random();
        private const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private static string GetShortCode(int length = 5) {
                var code = new StringBuilder();
                while(code.Length < length) {
                    code.Append(chars[random.Next(chars.Length)]);
                }
                return code.ToString();
        }

        [FunctionName("SendTokenEmail")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                Route = "SendTokenEmail/{email}/{regenerateTokens:bool?}")] HttpRequest req,
            [CosmosDB("passwordless", "users",
                ConnectionStringSetting = "CosmosDBConnection",
                SqlQuery = "select * from Users u where u.email = {email}")]
                IEnumerable<User> users,
            [CosmosDB(
                databaseName: "passwordless",
                collectionName: "users",
                ConnectionStringSetting = "CosmosDBConnection")] out dynamic newUser,
            [SendGrid(ApiKey="SendGridApiKey")] out SendGridMessage message,
            string email,
            ILogger log,
            bool? regenerateTokens)
        {
            log.LogInformation($"SendTokenEmail function processed a request (email: {email}{(regenerateTokens??false?", regenerate=true)":"")}).");

            var user = users?.FirstOrDefault();
            
            Console.WriteLine($"email={email}\r\nuser={(user == null ? "null" : user.Id)}");
            newUser = user != null && !(regenerateTokens ?? false)
                ? null
                : new {
                    id=(user?.Id ?? Guid.NewGuid().ToString()),
                    token=Guid.NewGuid(),
                    shortCode=GetShortCode(),
                    email=email
                };
            var token = newUser?.token ?? user.Token;
            var shortCode = newUser?.shortCode ?? user.ShortCode;
            Console.WriteLine($"token={token}\r\nshortCode={shortCode}");
            // Email token and short-code
            message = new SendGridMessage();
            message.AddTo(email);
            message.AddContent("text/html", @$"<html><body><h1>Passwordless Details</h1><div><h4>User Token <small><em>Keep private, this a full user token</em></small></h4><div>{token}</div></div><div><h4>Short-Code <small><em>(Use with email within apps)</em></small></h4><div>{shortCode}</div></div></body></html>");
            message.SetFrom(new EmailAddress("passwordless@code-consultants.co.uk"));
            message.SetSubject("Passwordless Codes");
            // Return a message confirming the action taken
            return new OkObjectResult(user == null ? "Created user with new token" :
                newUser != null ? "Regenerated tokens/codes" :
                "Re-sent tokens/codes");
        }
    }
}
