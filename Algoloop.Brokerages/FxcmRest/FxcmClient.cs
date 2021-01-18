﻿/*
 * Copyright 2021 Capnode AB
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Algoloop.Model;
using Newtonsoft.Json.Linq;
using QuantConnect.Logging;
using SocketIOClient;
using static Algoloop.Model.ProviderModel;

namespace Algoloop.Brokerages.FxcmRest
{
    /// <summary>
    /// https://github.com/fxcm/RestAPI
    /// </summary>
    public class FxcmClient : IDisposable
    {
        private const string _market = "fxcm-rest";
        private const string _baseUrlReal = "https://api.fxcm.com";
        private const string _baseUrlDemo = "https://api-demo.fxcm.com";
        private const string _mediatype = @"application/json";
        private const string _getModel = @"trading/get_model/?models=OpenPosition&models=ClosedPosition" +
            "&models=Order&models=Account&models=LeverageProfile&models=Properties";

        private bool _isDisposed;
        private readonly string _baseUrl;
        private readonly string _key;
        private readonly HttpClient _httpClient;
        private ManualResetEvent _hold = new ManualResetEvent(false);
        private SocketIO _socketio;

        public FxcmClient(ProviderModel.AccessType access, string key)
        {
            _baseUrl = access.Equals(AccessType.Demo) ? _baseUrlDemo : _baseUrlReal;
            _key = key;

            _httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(_mediatype));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "request");
        }

        public async Task<bool> LoginAsync()
        {
            var uri = new Uri(_baseUrl);
            var options = new SocketIOOptions
            {
                Query = new Dictionary<string, string> { { "access_token", _key } }
            };
            _socketio = new SocketIO(_baseUrl, options);
            _socketio.OnConnected += (sender, e) => _hold.Set();
            _socketio.OnError += (object sender, string e) => Log.Error(e);
            await _socketio.ConnectAsync();
            if (!_hold.WaitOne(TimeSpan.FromSeconds(10)))
            {
                return false;
            }
            string bearer = _socketio.Id + _key;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            return true;
        }

        public async Task<bool> LogoutAsync()
        {
            await _socketio.DisconnectAsync();
            return true;
        }

        public async Task<IReadOnlyList<AccountModel>> GetAccountsAsync()
        {
            Log.Trace("{0}: GetAccountsAsync", GetType().Name);
            string json = await GetAsync(_getModel);
            var accounts = new List<AccountModel>();
            JObject jo = JObject.Parse(json);
            JArray jAccounts = JArray.FromObject(jo["accounts"]);
            foreach (JToken jAccount in jAccounts)
            {
                var account = new AccountModel
                {
                    Id = jAccount["accountId"].ToString(),
                    Name = jAccount["accountName"].ToString(),
                };

                accounts.Add(account);
            }

            return accounts;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }

                _isDisposed = true;
            }
        }

        private async Task<string> GetAsync(string path)
        {
            string uri = _httpClient.BaseAddress + path;
            using (HttpResponseMessage response = await _httpClient.GetAsync(uri))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string message = $"GetAsync fail {(int)response.StatusCode} ({response.ReasonPhrase})";
                    Log.Error(message);
                    throw new ApplicationException(message);
                }

                return await response.Content.ReadAsStringAsync();
            }
        }

        private async Task<string> PostAsync(string path, string body)
        {
            string uri = _httpClient.BaseAddress + path;
            using (HttpResponseMessage response = await _httpClient.PostAsync(uri, new StringContent(body, Encoding.UTF8, _mediatype)))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string message = $"PostAsync fail {(int)response.StatusCode} ({response.ReasonPhrase})";
                    Log.Error(message);
                    throw new ApplicationException(message);
                }

                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}
