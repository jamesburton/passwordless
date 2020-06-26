# Passwordless (Azure Functions + Cosmos DB)
## Steps
* Create folder for project and navigate into it (e.g. `mkdir passwordless` then `cs passwordless`)
* Create function project `func init`
	* Select `dotnet`
?* Add the following to `host.json` to add extension bundles
?* OR Install individual extensions in `function.json` then install with `func extensions install`
?* Get azure storage key (create storage if required) from portal
* Update `local.settings.json` if not using local storage
* Install NuGet packages for required bindings:
	`dotnet add package Microsoft.Azure.WebJobs.Extensions.<BINDING_TYPE_NAME> --version <TARGET_VERSION>`
	* See supported bindings: https://docs.microsoft.com/en-us/azure/azure-functions/functions-triggers-bindings#supported-bindings
	* Check versions on NuGet e.g. https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.CosmosDB
	* e.g. `dotnet add package Microsoft.Azure.WebJobs.Extensions.CosmosDB --version 3.0.7`
* Configure CosmosDB to have a "passwordless" database
	* Add container "users"
		Fields: id, email, token, shortCode
* Add Functions
	* NB: Available Trigger templates: 
		* Blob trigger
		* Cosmos DB trigger
		* Event Grid trigger
		* HTTP trigger
		* Queue trigger
		* SendGrid
		* Service Bus Queue trigger
		* Service Bus Topic trigger
		* Timer trigger
	* Add `SendTokenEmail` (unsecured HTTP Trigger, emails token, with parameter to reset token and code)
		`func new --name SendTokenEmail --template "HTTP trigger"`
	* Add `GetUserById` (secured HTTP Trigger)
		`func new --name GetUserById --template "HTTP trigger"`
	* Add `GetToken` (secured HTTP Trigger, takes email and shortCode, returns token)
		`func new --name GetToken --template "HTTP trigger"`
	* Add `GetUser` (unsecured, takes token, returns id & email user object)
		`func new --name GetUser --template "HTTP trigger"`
* Test scaffolded routines with `func start`, check they are all listed
* Add `CosmosDBConnection` value to `local.settings.json` (with `Values`)
	* NB: Use connection string grabbed from Cosmos DB management interfaces
* Add `Models` folder
* Add `Models\User.cs` with the following:
```
namespace passwordless.Models
{
    public class User {
        public string Id { get; set; }
	public string Email { get; set; }
        public string Token { get; set; }
        public string ShortCode { get; set; }
    }
}
```
* Modify `SendTokenEmail.cs` to the following:
```
// In usings, add these usings:
using System.Collections.Generic;
using System.Linq;
using passwordless.Models;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
// At start of class, add these short-code generation routines
    	private static readonly Random random = new Random();
        private const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
	private static string GetShortCode(int length = 5) {
            var code = new StringBuilder();
            while(code.Length < length) {
                code.Append(chars[random.Next(chars.Length)]);
            }
            return code.ToString();
	}
// Within class, replace function with this:
        [FunctionName("SendTokenEmail")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "SendTokenEmail/{email}/{regenerateTokens:bool?}")] HttpRequest req,
            [CosmosDB("passwordless", "users",
                ConnectionStringSetting = "CosmosDBConnection",
                SqlQuery = "select * from Users u where u.email = {email}")]
                IEnumerable<User> users,
            [CosmosDB(
                databaseName: "passwordless",
                collectionName: "users",
                ConnectionStringSetting = "CosmosDBConnection")] out dynamic newUser,
            string email,
            ILogger log,
            bool? regenerateTokens)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var user = users?.FirstOrDefault();
            if(user == null) {
                //return new NotFoundObjectResult($"Found no users for {email}");
		newUser = new { id=Guid.NewGuid(), email=email, token=Guid.NewGuid(), shortCode=GetShortCode() };
                // TODO: Should email new tokens
                return new OkObjectResult($"Created new user for {email}");
            } else if(regenerateTokens ?? false) {
                user = users.First();
                newUser = new { id=user.Id, email=email, token=Guid.NewGuid(), shortCode=GetShortCode() };
                // TODO: Should email new tokens
                return new OkObjectResult($"Updated user for {email}");
            } else {
		newUser = null;
                // TODO: Should email existing tokens
		return new OkObjectResult($"Found {users.Count()} users for {email}");
            }
        }
```
* Test again with `func start`
	* Try accessing the SendTokenEmail endpoint with new and repeat users, with and without a boolean final parameter e.g.
		http://localhost:7071/api/SendTokenEmail/bob@example.com/
		=> http://localhost:7071/api/SendTokenEmail/bob@example.com/false
		=> http://localhost:7071/api/SendTokenEmail/bob@example.com/true
		=> http://localhost:7071/api/SendTokenEmail/alice@example.com
	* You should see a new user created, the same user matched, the user updated with new tokens, then a 2nd user created
* Now we will prepare to add SendGrid bindings
	* Add the package `dotnet add package Microsoft.Azure.WebJobs.Extensions.SendGrid`
	* Get an API token from SendGrid (register an app and generate a key if you do not have one, but their free tier should suffice here)
	* Add `SendGridApiKey` to `Values` in `local.settings.json`
        * Add using to SendTokenEmail.cs
```
using SendGrid.Helpers.Mail;
```
* Add an output binding to the SendTokenEmail method:
``` C#
// ... Cosmos DB Bindings
            [SendGrid(ApiKey="SendGridApiKey")] out SendGridMessage message,
// ... string email, ...
```
* Replace `SendTokenEmail` method body after `var user = users?.FirstOrDefault();` with:
```
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
```
* Re-test (`func start` and then retry the previous tests, check for emails with tokens & short-codes, noting down the final shortCode for your test email)
* Add login routines to `GetToken.cs`:
```
// Add usings
using System.Collections.Generic;
using System.Linq;
using passwordless.Models;
// Update method body
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
```
* Test new functions
	* `func start`
	* Navigate to `http://localhost:7071/api/GetToken/{email}/{shortCode}`
		... replacing email and shortCode with values from earlier tests
		... You should receive a GUID response
* Add GetUser routines:
```
// Add usings
using System.Linq;
using passwordless.Models;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
// Update method body
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
```
* Test `http://localhost:7071/api/GetUser` endpoint
	* Test via POST with "token" parameter in JSON body
	* Test via POST with "ptoken" header value
	* Test via GET with "ptoken" header value
	* Test via POST with "ptoken" cookie value
	* Test via GET with "ptoken" cookie value
	* Test no-details request failure (no-content) e.g. Browse to `http://localhost:7071/api/GetUser`
* Add `Models/IdModel.cs` (binding model) with the following:
```
using Newtonsoft.Json;

namespace passwordless.Models
{
    public class IdModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }
}
```
* Update `GetUserById.cs` with the following:
```
// Add these usings
// Update the method to this
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
```
* Test `http://localhost:7071/api/GetUserById` by POSTing the following:
``` json
{
    "id": "<IdFromCreatedUser>"
}
```
	... You should receive your JSON user object back
* Make SendTokenEmail method anonymous by changing it's HttpTrigger to this:
```
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                Route = "SendTokenEmail/{email}/{regenerateTokens:bool?}")] HttpRequest req,
```
* Set-up Continuous Integration (Visual Studio)
	* Coming Soon
* Set-up Continuous Integration (CLI)
	* Coming Soon

## TODO

* Add Continous Integration deployment notes
* Add C# Library
* Add C# Library Usage
	* ASP.NET Core Usage
	* Blazor Usage
* Add JS Library
	* Use client-side
	* Use within node back-end
	* MST Helper types/middleware
## Summary

We now have an API to generate passwordless tokens and codes, a method to accept and email and short code to get a token back, and methods to get the user either by token (GetUser) or by id (GetUserById).  The user can either save a token as their full access credentials, or this could be hidden and the user would simply enter the short-code and let the server fetch the token from that and their email address.

The full project is available at (https://github.com/jamesburton/passwordless.git)[https://github.com/jamesburton/passwordless.git]