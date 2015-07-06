﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ASPMVCProducts_WPFClient
{
	public class HttpJSONRequester
	{
		public CookieContainer Cookies { get; private set; }
		HttpClientHandler mClientHandler;
		public HttpJSONRequester()
		{
			Cookies = new CookieContainer();
			mClientHandler = new HttpClientHandler()
			{
				CookieContainer = Cookies,
				UseCookies = true,
				UseDefaultCredentials = false
			};
		}
		public async Task<TResponse> Get<TResponse>(string aBaseURL, string aRequestURL, IEnumerable<KeyValuePair<string, string>> aRequestHeaders = null)
		{
			using (var lClient = new HttpClient(mClientHandler, false))
			{
				lClient.BaseAddress = new Uri(aBaseURL);
				lClient.DefaultRequestHeaders.Accept.Clear();
				lClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				if (aRequestHeaders != null)
				{
					foreach (var lPair in aRequestHeaders)
					{
						if (!lClient.DefaultRequestHeaders.Contains(lPair.Key))
							lClient.DefaultRequestHeaders.Add(lPair.Key, lPair.Value);
					}
				}
				HttpResponseMessage lResponse = await lClient.GetAsync(aRequestURL);
				if (lResponse.IsSuccessStatusCode)
				{
					return await lResponse.Content.ReadAsAsync<TResponse>();
				}
				return default(TResponse);
			}

		}

		public async Task<HttpResponseMessage> Post(string aBaseURL, string aRequestURL, IEnumerable<KeyValuePair<string, string>> aRequestHeaders = null)
		{
			return await Post<string>(aBaseURL, aRequestURL, string.Empty, aRequestHeaders);
		}

		public async Task<TResponse> Post<TRequest, TResponse>(string aBaseURL, string aRequestURL, TRequest aData, IEnumerable<KeyValuePair<string, string>> aRequestHeaders = null)
		{
			var lResponse = await Post<TRequest>(aBaseURL, aRequestURL, aData, aRequestHeaders);
			if (lResponse.IsSuccessStatusCode)
			{
				return await lResponse.Content.ReadAsAsync<TResponse>();
			}
			return default(TResponse);
		}

		public async Task<HttpResponseMessage> Post<TRequest>(string aBaseURL, string aRequestURL, TRequest aData, IEnumerable<KeyValuePair<string, string>> aRequestHeaders = null)
		{
			using (var lClient = new HttpClient(mClientHandler, false))
			{
				lClient.BaseAddress = new Uri(aBaseURL);
				lClient.DefaultRequestHeaders.Accept.Clear();
				lClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				if (aRequestHeaders != null)
				{
					foreach (var lPair in aRequestHeaders)
					{
						if (!lClient.DefaultRequestHeaders.Contains(lPair.Key))
							lClient.DefaultRequestHeaders.Add(lPair.Key, lPair.Value);
					}
				}
				string lPostBody = JsonConvert.SerializeObject(aData);
				var lContent = new StringContent(lPostBody, Encoding.UTF8, "application/json");
				return await lClient.PostAsync(aRequestURL, lContent);
			}

		}

		
	}
}
