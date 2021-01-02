using System;
using System.Net;
using System.Text.RegularExpressions;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;

namespace ValorantBotCS
{
	class Program
	{
		public static string AccessToken { get; set; }
		public static string EntitlementToken { get; set; }
		public static string username { get; set; }
		public static string password { get; set; }
		public static string UserID { get; set; }
		public static string region { get; set; }
		public static Dictionary<int, string> rankDict = new Dictionary<int, string>(){
			{ 0, "Unrated"},
			{ 1, "Unknown 1"},
			{ 2, "Unknown 2"},
			{ 3, "Iron 1"},
			{ 4, "Iron 2"},
			{ 5, "Iron 3"},
			{ 6, "Bronze 1"},
			{ 7, "Bronze 2"},
			{ 8, "Bronze 3"},
			{ 9, "Silver 1"},
			{ 10, "Silver 2"},
			{ 11, "Silver 3"},
			{ 12, "Gold 1"},
			{ 13, "Gold 2"},
			{ 14, "Gold 3"},
			{ 15, "Platinum 1"},
			{ 16, "Platinum 2"},
			{ 17, "Platinum 3"},
			{ 18, "Diamond 1"},
			{ 19, "Diamond 2"},
			{ 20, "Diamond 3"},
			{ 21, "Immortal 1"},
			{ 22, "Immortal 2"},
			{ 23, "Immortal 3"},
			{ 24, "Radiant"},
			};
		public DiscordSocketClient _client;


		public static void Main(string[] args)
			=> new Program().MainAsync().GetAwaiter().GetResult();

		 private async Task MainAsync()
		{
			_client = new DiscordSocketClient();
			_client.MessageReceived += CommandHandler;
			_client.Log += Log;

			//  You can assign your bot token to a string, and pass that in to connect.
			//  This is, however, insecure, particularly if you plan to have your code hosted in a public repository.
			var token = "discord_token";

			// Some alternative options would be to keep your token in an Environment Variable or a standalone file.
			// var token = Environment.GetEnvironmentVariable("NameOfYourEnvironmentVariable");
			// var token = File.ReadAllText("token.txt");
			// var token = JsonConvert.DeserializeObject<DiscordToken>(File.ReadAllText("config.json")).Token;

			await _client.LoginAsync(TokenType.Bot, token);
			await _client.SetGameAsync("-help");
			await _client.StartAsync();
			Console.WriteLine("Bot has started...");
			// Block this task until the program is closed.
			await Task.Delay(-1);
		}
		 private Task Log(LogMessage msg)
		{
			Console.WriteLine(msg.ToString());
			return Task.CompletedTask;
		}

		private Task CommandHandler(SocketMessage message)
		{
			string command = "";
			int lengthOfCommand = -1;

			//ignore message if does not start with prefix
			if (!message.Content.StartsWith('-') || message.Author.IsBot)
				return Task.CompletedTask;

			if (message.Content.Contains(' '))
				lengthOfCommand = message.Content.IndexOf(' ');
			else
				lengthOfCommand = message.Content.Length;

			Console.WriteLine("message received from " + message.Author.ToString());

			command = message.Content.Substring(1, lengthOfCommand - 1);

			//commands
			if (command.Equals("help"))
				message.Author.SendMessageAsync($@"{message.Author.Mention}, To check your rank, reply with the command `-login yourusername yourpassword`");

			if (command.Equals("login") && message.Channel.GetType().Name.Equals("SocketDMChannel"))
			{
				string[] credentials = message.ToString().Split(' ');
				if(credentials.Length < 3)
				{
					return Task.CompletedTask;
				}
				username = credentials[1];
				password = credentials[2];
				region = "na";
				Login(message);
				CheckRankedUpdates(message);
			}
			return Task.CompletedTask;
		}

		private void Login(SocketMessage message)
		{
			
			CookieContainer cookie = new CookieContainer();
			Authentication.GetAuthorization(cookie);

			var authJson = JsonConvert.DeserializeObject(Authentication.Authenticate(cookie, username, password));
			JToken authObj = JObject.FromObject(authJson);

			string authURL = "";

			if (authObj["error"] != null)
			{
				message.Author.SendMessageAsync("Invalid credentials! Make sure you entered the correct username and password!");
				return;
			}
			else
			{
				authURL = authObj["response"]["parameters"]["uri"].Value<string>();
			}

			var access_tokenVar = Regex.Match(authURL, @"access_token=(.+?)&scope=").Groups[1].Value;
			AccessToken = $"{access_tokenVar}";

			RestClient client = new RestClient(new Uri("https://entitlements.auth.riotgames.com/api/token/v1"));
			RestRequest request = new RestRequest(Method.POST);

			request.AddHeader("Authorization", $"Bearer {AccessToken}");
			request.AddJsonBody("{}");

			string response = client.Execute(request).Content;
			var entitlement_token = JsonConvert.DeserializeObject(response);
			JToken entitlement_tokenObj = JObject.FromObject(entitlement_token);

			EntitlementToken = entitlement_tokenObj["entitlements_token"].Value<string>();


			RestClient userid_client = new RestClient(new Uri("https://auth.riotgames.com/userinfo"));
			RestRequest userid_request = new RestRequest(Method.POST);

			userid_request.AddHeader("Authorization", $"Bearer {AccessToken}");
			userid_request.AddJsonBody("{}");

			string userid_response = userid_client.Execute(userid_request).Content;
			dynamic userid = JsonConvert.DeserializeObject(userid_response);
			JToken useridObj = JObject.FromObject(userid);

			UserID = useridObj["sub"].Value<string>();

			message.Author.SendMessageAsync($"{username} successfully logged in! Fetching last game(s)");
		
		}
		private DateTime UnixStampToDateTime(long unixTimeStamp)
		{
			var time = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(unixTimeStamp).ToLocalTime();
			return time;
		}

		private string GetUsernameFromID(string playerID)
		{
			string url = "https://pd.NA.a.pvp.net/name-service/v2/players";
			RestClient client = new RestClient(url);

			RestRequest request = new RestRequest(Method.PUT);

			request.AddHeader("Authorization", $"Bearer {AccessToken}");
			request.AddHeader("X-Riot-Entitlements-JWT", EntitlementToken);
			Console.WriteLine(UserID);


			request.AddJsonBody(new { PLAYERID = UserID});
			return client.Execute(request).Content;
		}
		private void CheckRankedUpdates(SocketMessage message)
		{
			try
			{
				RestClient ranked_client = new RestClient(new Uri($"https://pd.{region}.a.pvp.net/mmr/v1/players/{UserID}/competitiveupdates?startIndex=0&endIndex=20"));
				RestRequest ranked_request = new RestRequest(Method.GET);

				ranked_request.AddHeader("Authorization", $"Bearer {AccessToken}");
				ranked_request.AddHeader("X-Riot-Entitlements-JWT", EntitlementToken);

				IRestResponse rankedresp = ranked_client.Get(ranked_request);
				if (rankedresp.IsSuccessful)
				{
					dynamic RankedJson = JsonConvert.DeserializeObject<JObject>(rankedresp.Content);
					// Debugging IGNORE
					//Console.WriteLine(RankedJson);
					var store = RankedJson["Matches"];

					StringBuilder output = new StringBuilder();
					output.Append("```"); //code format for discord

					int rankNumber = 0;
					int currentRp = 0;
					int i = 1;
					foreach (var game in store)
					{
						if (game["CompetitiveMovement"] != "MOVEMENT_UNKNOWN" && game["CompetitiveMovement"] != "PROMOTED" && i <= 5)
						{
							//only update currentrp and rank with the most recent comp match
							if(i == 1)
							{
								rankNumber = game["TierAfterUpdate"];
								currentRp = game["TierProgressAfterUpdate"];
							}
							 
							//get map
							string mapId = game["MapID"];
							string map = mapId.Substring(mapId.LastIndexOf("/") + 1);

							//get date of match
							long timeStamp = game["MatchStartTime"];
							DateTime date = UnixStampToDateTime(timeStamp);


							//points calculation
							int before = game["TierProgressBeforeUpdate"];
							int after = game["TierProgressAfterUpdate"];

							int num = after - before;

							output.Append($"\n\nGame: {i}\nMap: {map}\nDate: {date}\npoints before: {before}\npoints after: {after}\ndifference: {num}\n");
							i++;
						}
						else if (game["CompetitiveMovement"] == "PROMOTED" && i == 1)
						{
							rankNumber = game["TierAfterUpdate"];
							currentRp = game["TierProgressAfterUpdate"];
						}
						else if(game["CompetitiveMovement"] == "PROMOTED" || i > 5)
						{
							//Console.WriteLine(output.ToString());
							goto AfterLoop; //break out of loop, we only want matches after promotion for point calculations
						}
					}
					AfterLoop:
						i = 1; //reset game counter
						string result;
						if (rankDict.TryGetValue(rankNumber, out result))
							{
							//insert at the front of string
							output.Insert(3, "Rank: " + result + " | Current RP: " + currentRp + " | Total Elo: " + ((rankNumber * 100) - 300 + currentRp));
							}
						output.Append("```"); //close code format
						message.Channel.SendMessageAsync(output.ToString());
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
				throw;
			}
		}
	}
}
