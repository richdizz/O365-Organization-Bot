using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using AuthBot;
using AuthBot.Dialogs;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using Newtonsoft.Json.Linq;

namespace OrgBot.Dialogs
{
    [Serializable]
    public class ActionDialog : IDialog<string>
    {

        private static Lazy<string> mode = new Lazy<string>(() => ConfigurationManager.AppSettings["ActiveDirectory.Mode"]);
        private static Lazy<string> activeDirectoryEndpointUrl = new Lazy<string>(() => ConfigurationManager.AppSettings["ActiveDirectory.EndpointUrl"]);
        private static Lazy<string> activeDirectoryTenant = new Lazy<string>(() => ConfigurationManager.AppSettings["ActiveDirectory.Tenant"]);
        private static Lazy<string> activeDirectoryResourceId = new Lazy<string>(() => ConfigurationManager.AppSettings["ActiveDirectory.ResourceId"]);
        private static Lazy<string> redirectUrl = new Lazy<string>(() => ConfigurationManager.AppSettings["ActiveDirectory.RedirectUrl"]);
        private static Lazy<string> clientId = new Lazy<string>(() => ConfigurationManager.AppSettings["ActiveDirectory.ClientId"]);
        private static Lazy<string> clientSecret = new Lazy<string>(() => ConfigurationManager.AppSettings["ActiveDirectory.ClientSecret"]);
        private static Lazy<string> resourceId = new Lazy<string>(() => ConfigurationManager.AppSettings["ActiveDirectory.ResourceId"]);

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
        }

        public async Task MessageReceivedAsync(IDialogContext context, IAwaitable<Message> item)
        {
            var message = await item;

            context.UserData.SetValue(ContextConstants.CurrentMessageFromKey, message.From);
            context.UserData.SetValue(ContextConstants.CurrentMessageToKey, message.To);
            // await context.PostAsync(message.Text);


            if (string.Equals(message.Text, "help", StringComparison.OrdinalIgnoreCase))
            {
                await context.PostAsync("Hello...just give me the name of someone in your company and I will give you their full organization structure.");

                context.Wait(this.MessageReceivedAsync);
            }
            else if (string.Equals(message.Text, "logout", StringComparison.OrdinalIgnoreCase))
            {
                await context.Logout();
                context.Wait(this.MessageReceivedAsync);
            }
            else
            {   //Assume this is a query for organization structure
                if (string.IsNullOrEmpty(await context.GetAccessToken(resourceId.Value)))
                {
                    //We can't get an access token, so let's try to log the user in
                    await context.Forward(new AzureAuthDialog(resourceId.Value), this.ResumeAfterAuth, message, CancellationToken.None);

                }
                else
                {
                    // Search for the user
                    var results = await searchUser(message.Text, await context.GetAccessToken(resourceId.Value));
                    if (results.Count == 0)
                    {
                        //none found
                        await context.PostAsync("No results found...try checking the spelling or just type the beginning of the name (ex: Barak Oba).");
                        context.Wait(this.MessageReceivedAsync);
                    }
                    else if (results.Count > 1)
                    {
                        //let them choose
                        PromptDialog.Choice(context, userSelected, results, "Which user do you want to explore?");
                    }
                    else
                    {
                        //process the org for the user
                        var user = results[0];
                        await outputUser(context, user);
                    }
                }
            }
        }

        private async Task outputUser(IDialogContext context, Models.User user)
        {
            //get management structure and direct reports
            user.Manager = await getManager(user, await context.GetAccessToken(resourceId.Value));
            user.DirectReports = await getDirectReports(user, await context.GetAccessToken(resourceId.Value));

            //flatten structure
            var pointer = user;
            List<Models.User> org = new List<Models.User>();
            org.Add(pointer.Clone());
            while (pointer.Manager != null)
            {
                org.Insert(0, pointer.Manager.Clone());
                pointer = pointer.Manager;
            }
            int i = 0;
            for (i = 0; i < org.Count; i++)
            {
                if (i == 0)
                    org[i].IndentedDisplayName = org[i].DisplayName;
                else if (i == org.Count - 1)
                    org[i].IndentedDisplayName = indent((i - 1) * 2) + "* **" + org[i].DisplayName + "**";
                else
                    org[i].IndentedDisplayName = indent((i - 1) * 2) + "* " + org[i].DisplayName;
            }
            foreach (var directReport in user.DirectReports)
            {
                var dr = directReport.Clone();
                dr.IndentedDisplayName = indent(i * 2) + "* " + dr.DisplayName;
                org.Add(dr);
            }
            var result = String.Join("\n\n", org.Select(x => x.IndentedDisplayName));

            //check for any results
            await context.PostAsync(result);
            context.Wait(MessageReceivedAsync);
        }

        private async Task userSelected(IDialogContext context, IAwaitable<Models.User> user)
        {
            var u = await user;
            await outputUser(context, u);
        }


        private string indent(int spaces)
        {
            string indent = "";
            for (var i = 0; i < spaces; i++)
                indent += " ";
            return indent;
        }

        private async Task<Models.User> getManager(Models.User user, string token)
        {
            Models.User manager = null;

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var result = await client.GetAsync("https://graph.microsoft.com/v1.0/users/" + user.Id + "/manager?$select=displayName,id");

                if (result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var resultString = await result.Content.ReadAsStringAsync();
                    var item = JObject.Parse(resultString);
                    manager = new Models.User();
                    manager.DisplayName = item.Value<string>("displayName");
                    manager.Id = item.Value<string>("id");
                    manager.Manager = await getManager(manager, token);
                }
            }

            return manager;
        }

        private async Task<List<Models.User>> getDirectReports(Models.User user, string token)
        {
            List<Models.User> results = new List<Models.User>();

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var result = await client.GetAsync("https://graph.microsoft.com/v1.0/users/" + user.Id + "/directReports?$select=displayName,id");
                var resultString = await result.Content.ReadAsStringAsync();

                var jResult = JObject.Parse(resultString);
                JArray jFiles = (JArray)jResult["value"];
                foreach (JObject item in jFiles)
                {
                    Models.User u = new Models.User();
                    u.DisplayName = item.Value<string>("displayName");
                    u.Id = item.Value<string>("id");
                    results.Add(u);
                }
            }

            return results;
        }

        private async Task<List<Models.User>> searchUser(string name, string token)
        {
            List<Models.User> results = new List<Models.User>();

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var result = await client.GetAsync("https://graph.microsoft.com/v1.0/users?$filter=startswith(displayName,'" + name + "')&$select=displayName,id");
                var resultString = await result.Content.ReadAsStringAsync();

                var jResult = JObject.Parse(resultString);
                JArray jFiles = (JArray)jResult["value"];
                foreach (JObject item in jFiles)
                {
                    Models.User user = new Models.User();
                    user.DisplayName = item.Value<string>("displayName");
                    user.Id = item.Value<string>("id");
                    results.Add(user);
                }
            }

            return results;
        }

        private async Task ResumeAfterAuth(IDialogContext context, IAwaitable<string> result)
        {
            var message = await result;
            await context.PostAsync(message);

            await context.PostAsync("If you want me to log you off, just say \"logout\". Now who would you like to see in the organization?");

            context.Wait(MessageReceivedAsync);
        }
    }
}
