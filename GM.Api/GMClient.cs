﻿using JWT;
using JWT.Algorithms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GM.Api
{
    public class GMClient
    {
        //TODO: handle token renewals
        //TODO: maybe throw exceptions?

        string _clientId;
        string _deviceId;
        JwtTool _jwtTool;
        string _apiUrl;
        string _host;

        HttpClient _client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = true, AutomaticDecompression = System.Net.DecompressionMethods.GZip });


        LoginData _loginData = null;

        bool _isUpgraded = false;
        bool _isConnected = false;

        public GMClient(string clientId, string deviceId, string clientSecret, string apiUrl)
        {
            _clientId = clientId;
            _deviceId = deviceId;
            _jwtTool = new JwtTool(clientSecret);
            _apiUrl = apiUrl;
            var uri = new Uri(_apiUrl);
            _host = uri.Host;


            _client.DefaultRequestHeaders.AcceptEncoding.SetValue("gzip");
            _client.DefaultRequestHeaders.Accept.SetValue("application/json");
            _client.DefaultRequestHeaders.AcceptLanguage.SetValue("en-US");
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("okhttp/3.9.0");
            _client.DefaultRequestHeaders.Host = _host;
            _client.DefaultRequestHeaders.MaxForwards = 10;
            _client.DefaultRequestHeaders.ExpectContinue = false;
        }





        async Task<Commandresponse> VehicleConnect(string vin)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/v1/account/vehicles/{vin}/commands/connect");
            req.Content = new StringContent("{}", Encoding.UTF8, "application/json");



            var response = await _client.SendAsync(req);



            if (response.IsSuccessStatusCode)
            {
                var respString = await response.Content.ReadAsStringAsync();
                var respObj = JsonConvert.DeserializeObject<CommandResponseRoot>(respString);
                if (respObj == null || respObj.commandResponse == null) return null;
                return respObj.commandResponse;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return null;
            }

        }


        async Task<bool> UpgradeToken(string pin)
        {
            var payload = new UpgradeTokenPayload()
            {
                client_id = _clientId,
                device_id = _deviceId,
                credential = pin,
                credential_type = "PIN",
                nonce = helpers.GenerateNonce(),
                timestamp = DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK")
            };

            var token = _jwtTool.EncodeToken(payload);

            var req = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/v1/oauth/token/upgrade");
            req.Content = new StringContent(token, Encoding.UTF8, "text/plain");

            var response = await _client.SendAsync(req);

            if (response.IsSuccessStatusCode)
            {
                _isUpgraded = true;
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return false;
            }
        }


        public async Task<bool> Login(string username, string password)
        {
            var payload = new LoginPayload()
            {
                client_id = _clientId,
                device_id = _deviceId,
                grant_type = "password",
                nonce = helpers.GenerateNonce(),
                password = password,
                scope = "onstar gmoc commerce user_trailer msso",
                timestamp = DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK"),
                username = username
            };

            var token = _jwtTool.EncodeToken(payload);


            var req = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/v1/oauth/token");
            req.Headers.Authorization = null;
            req.Content = new StringContent(token, Encoding.UTF8, "text/plain");

            var response = await _client.SendAsync(req);


            string rawResponseToken = null;

            if (response.IsSuccessStatusCode)
            {
                rawResponseToken = await response.Content.ReadAsStringAsync();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
            }

            if (string.IsNullOrEmpty(rawResponseToken))
            {
                return false;
            }

            var loginTokenData = _jwtTool.DecodeTokenToObject<LoginData>(rawResponseToken);

            _loginData = loginTokenData;
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _loginData.access_token);
            return true;
        }

        public async Task<bool> RefreshToken()
        {
            var payload = new RefreshTokenPayload()
            {
                client_id = _clientId,
                device_id = _deviceId,
                grant_type = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                nonce = helpers.GenerateNonce(),
                scope = "onstar gmoc commerce user_trailer",
                timestamp = DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK"),
                assertion = _loginData.id_token
            };

            var token = _jwtTool.EncodeToken(payload);


            var req = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/v1/oauth/token");
            req.Headers.Authorization = null;
            req.Content = new StringContent(token, Encoding.UTF8, "text/plain");

            var response = await _client.SendAsync(req);


            string rawResponseToken = null;

            if (response.IsSuccessStatusCode)
            {
                rawResponseToken = await response.Content.ReadAsStringAsync();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
            }

            if (string.IsNullOrEmpty(rawResponseToken))
            {
                return false;
            }

            /*{
  "access_token": ,
  "token_type": "Bearer",
  "expires_in": 1800,
  "scope": "user_trailer onstar commerce  gmoc role_owner",
  "user_info": {
    "RemoteUserId": "",
    "country": ""
  }
}*/
// Not sure if the scope needs to be updated, as msso has been removed in the refresh request

            var refreshData = _jwtTool.DecodeTokenToObject<LoginData>(rawResponseToken);

            _loginData.access_token = refreshData.access_token;
            _loginData.IssuedUtc = refreshData.IssuedUtc;
            _loginData.expires_in = refreshData.expires_in;

            //should we assume the upgrade status is broken?


            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _loginData.access_token);
            return true;
        }




        #region Commands



        public async Task<Commandresponse> InitiateCommand(string vin, string pin, string command)
        {
            if (!_isConnected)
            {
                await VehicleConnect(vin);
                _isConnected = true;
            }

            await Task.Delay(500);

            if (!_isUpgraded)
            {
                if (!await UpgradeToken(pin)) return null;
            }


            //


            JObject reqObj;

            if (command == "lockDoor" || command == "unlockDoor")
            {
                reqObj = new JObject()
                {
                    [$"{command}Request"] = new JObject()
                    {
                        ["delay"] = 0
                    }
                };
            }
            else if (command == "alert")
            {
                reqObj = new JObject()
                {
                    //TODO: these parameters may be controllable :D
                    [$"{command}Request"] = new JObject()
                    {
                        ["action"] = new JArray() { "Honk", "Flash" },
                        ["delay"] = 0,
                        ["duration"] = 1,
                        ["override"] = new JArray() { "DoorOpen", "IgnitionOn" }
                    }
                };
            }
            else if (command == "diagnostics")
            {
                reqObj = new JObject()
                {
                    [$"{command}Request"] = new JObject()
                    {
                        ["diagnosticItem"] = new JArray(DiagnosticRequestRoot.DefaultItems)
                    }
                };
            }
            else
            {
                reqObj = new JObject();
            }




            var req = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/v1/account/vehicles/{vin}/commands/{command}");

            req.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(reqObj), Encoding.UTF8, "application/json");

            var response = await _client.SendAsync(req);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return null;
            }

            var commandResult = await response.Content.ReadAsAsync<CommandResponseRoot>();

            return commandResult.commandResponse;
        }


        public async Task<Commandresponse> WaitForCommandCompletion(string statusUrl)
        {
            int nullResponseCount = 0;

            while (true)
            {
                await Task.Delay(5000);
                var result = await PollCommandStatus(statusUrl);
                if (result == null)
                {
                    nullResponseCount++;
                    if (nullResponseCount > 5) return null;
                }
                if ("inProgress".Equals(result.status, StringComparison.OrdinalIgnoreCase)) continue;
                return result;
            }
        }


        async Task<Commandresponse> InitiateCommandAndWait(string vin, string pin, string command)
        {
            var result = await InitiateCommand(vin, pin, command);
            var endStatus = await WaitForCommandCompletion(result.url);
            return endStatus;
        }

        async Task<bool> InitiateCommandAndWaitForSuccess(string vin, string pin, string command)
        {
            var result = await InitiateCommandAndWait(vin, pin, command);
            if (result == null) return false;
            if ("success".Equals(result.status, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        async Task<Commandresponse> PollCommandStatus(string statusUrl)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{statusUrl}?units=METRIC");

            var response = await _client.SendAsync(req);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<CommandResponseRoot>();
                return result.commandResponse;
            }
            else
            {
                return null;
            }
        }


        public async Task<Diagnosticresponse[]> GetDiagnostics(string vin, string pin)
        {
            var result = await InitiateCommandAndWait(vin, pin, "diagnostics");
            if (result == null) return null;
            if ("success".Equals(result.status, StringComparison.OrdinalIgnoreCase))
            {
                return result.body.diagnosticResponse;
            }
            else
            {
                return null;
            }



        }



        public async Task<bool> LockDoor(string vin, string pin)
        {
            return await InitiateCommandAndWaitForSuccess(vin, pin, "lockDoor");
        }

        public async Task<bool> UnlockDoor(string vin, string pin)
        {
            return await InitiateCommandAndWaitForSuccess(vin, pin, "unlockDoor");
        }

        public async Task<bool> Start(string vin, string pin)
        {
            return await InitiateCommandAndWaitForSuccess(vin, pin, "start");
        }

        public async Task<bool> CancelStart(string vin, string pin)
        {
            return await InitiateCommandAndWaitForSuccess(vin, pin, "cancelStart");
        }


        public async Task<bool> Alert(string vin, string pin)
        {
            return await InitiateCommandAndWaitForSuccess(vin, pin, "alert");
        }


        public async Task<bool> CancelAlert(string vin, string pin)
        {
            return await InitiateCommandAndWaitForSuccess(vin, pin, "cancelAlert");
        }



        #endregion
    }
}