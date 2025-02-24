﻿using IdentityModel.Client;
using IdentityModel.OidcClient;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ConsoleClientWithBrowser
{
    public class Program
    {
        static string _authority = "https://auth.simplisafe.com/";
        static string _api = "https://demo.duendesoftware.com/api/test";

        static OidcClient _oidcClient;
        static HttpClient _apiClient = new HttpClient { BaseAddress = new Uri(_api) };

        public static void Main(string[] args) => MainAsync().GetAwaiter().GetResult();

        public static async Task MainAsync()
        {
            Console.WriteLine("+-----------------------+");
            Console.WriteLine("|  Sign in with OIDC    |");
            Console.WriteLine("+-----------------------+");
            Console.WriteLine("");
            Console.WriteLine("Press any key to sign in...");
            Console.ReadKey();

            await Login();
        }

        private static async Task Login()
        {
            // create a redirect URI using an available port on the loopback address.
            // requires the OP to allow random ports on 127.0.0.1 - otherwise set a static port
            var browser = new SystemBrowser();
            //string redirectUri = string.Format($"http://127.0.0.1:{browser.Port}");
            string redirectUri = string.Format($"com.simplisafe.mobile://auth.simplisafe.com/ios/com.simplisafe.mobile/callback");

            //var options = new OidcClientOptions
            //{
            //    Authority = _authority,
            //    ClientId = "interactive.public",
            //    RedirectUri = redirectUri,
            //    Scope = "openid profile api",
            //    FilterClaims = false,
            //    Browser = browser,
            //};

            var options = new OidcClientOptions
            {
                Authority = _authority,
                ClientId = "42aBZ5lYrVW12jfOuu3CQROitwxg9sN5",
                RedirectUri = redirectUri,
                Scope = "offline_access email openid https://api.simplisafe.com/scopes/user:platform",
                FilterClaims = false,
                Browser = browser,
            };

            var serilog = new LoggerConfiguration()
                .MinimumLevel.Error()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}")
                .CreateLogger();

            options.LoggerFactory.AddSerilog(serilog);

            _oidcClient = new OidcClient(options);
            var parameters = new Parameters();
            parameters.Add("audience", "https://api.simplisafe.com/");
            parameters.Add("auth0Client", "eyJuYW1lIjoiQXV0aDAuc3dpZnQiLCJlbnYiOnsiaU9TIjoiMTUuMCIsInN3aWZ0IjoiNS54In0sInZlcnNpb24iOiIxLjMzLjAifQ");
            //parameters.Add("audience", "https://api.simplisafe.com/");
            //parameters.Add("auth0Client", "eyJuYW1lIjoiQXV0aDAuc3dpZnQiLCJlbnYiOnsiaU9TIjoiMTUuMCIsInN3aWZ0IjoiNS54In0sInZlcnNpb24iOiIxLjMzLjAifQ"); parameters.Add("audience", "https://api.simplisafe.com/");
            var state = await _oidcClient.PrepareLoginAsync(parameters);

            Console.WriteLine("Use this login URL in the browser");
            Console.WriteLine(state.StartUrl);
            Console.WriteLine("Enter the callback url from the browser's console it starts with com.simplesafe");
            var callbackUrl = Console.ReadLine();
            var response = await _oidcClient.ProcessResponseAsync(callbackUrl, state);
            Console.WriteLine("--- Access token ---");
            Console.WriteLine(response.AccessToken);
            Console.WriteLine("--- Refresh Token ---");
            Console.WriteLine(response.RefreshToken);

            ////var request = new LoginRequest();
            ////request.FrontChannelExtraParameters = parameters;
            ////var result = await _oidcClient.LoginAsync(new LoginRequest());

            //ShowResult(result);
            //await NextSteps(result);
            ;
        }

        private static void ShowResult(LoginResult result)
        {
            if (result.IsError)
            {
                Console.WriteLine("\n\nError:\n{0}", result.Error);
                return;
            }

            Console.WriteLine("\n\nClaims:");
            foreach (var claim in result.User.Claims)
            {
                Console.WriteLine("{0}: {1}", claim.Type, claim.Value);
            }

            Console.WriteLine($"\nidentity token: {result.IdentityToken}");
            Console.WriteLine($"access token:   {result.AccessToken}");
            Console.WriteLine($"refresh token:  {result?.RefreshToken ?? "none"}");
        }

        private static async Task NextSteps(LoginResult result)
        {
            var currentAccessToken = result.AccessToken;
            var currentRefreshToken = result.RefreshToken;

            var menu = "  x...exit  c...call api   ";
            if (currentRefreshToken != null) menu += "r...refresh token   ";

            while (true)
            {
                Console.WriteLine("\n\n");

                Console.Write(menu);
                var key = Console.ReadKey();

                if (key.Key == ConsoleKey.X) return;
                if (key.Key == ConsoleKey.C) await CallApi(currentAccessToken);
                if (key.Key == ConsoleKey.R)
                {
                    var refreshResult = await _oidcClient.RefreshTokenAsync(currentRefreshToken);
                    if (refreshResult.IsError)
                    {
                        Console.WriteLine($"Error: {refreshResult.Error}");
                    }
                    else
                    {
                        currentRefreshToken = refreshResult.RefreshToken;
                        currentAccessToken = refreshResult.AccessToken;

                        Console.WriteLine("\n\n");
                        Console.WriteLine($"access token:   {refreshResult.AccessToken}");
                        Console.WriteLine($"refresh token:  {refreshResult?.RefreshToken ?? "none"}");
                    }
                }
            }
        }

        private static async Task CallApi(string currentAccessToken)
        {
            _apiClient.SetBearerToken(currentAccessToken);
            var response = await _apiClient.GetAsync("");

            if (response.IsSuccessStatusCode)
            {
                var json = JArray.Parse(await response.Content.ReadAsStringAsync());
                Console.WriteLine("\n\n");
                Console.WriteLine(json);
            }
            else
            {
                Console.WriteLine($"Error: {response.ReasonPhrase}");
            }
        }
    }
}
