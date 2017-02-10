﻿using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Larais.NuGetApp
{
    public class NuGetServerService
    {
        private readonly HttpClient httpClient;

        private IReadOnlyDictionary<string, string> feeds;

        public NuGetServerService()
        {
            httpClient = new HttpClient();
            RefreshFeeds();
        }

        public void RefreshFeeds()
        {
            var settings = (SettingsManager)Startup.ServiceProvider.GetService(typeof(SettingsManager));

            feeds = settings.Feeds;
        }

        public async Task ForwardCall(HttpContext context, PathString feedPath, bool isFeedCall)
        {
            string feedName = feedPath.Value.Substring(1);
            string action = string.Empty;

            int firstSlash = feedName.IndexOf('/');
            if (firstSlash == feedName.Length - 1)
            {
                feedName = feedName.Substring(0, feedName.Length - 1);
            }
            else if (firstSlash != -1)
            {
                action = feedName.Substring(firstSlash);
                feedName = feedName.Substring(0, firstSlash);
            }

            if (!feeds.ContainsKey(feedName))
            {
                context.Response.StatusCode = 404;
                return;
            }

            string targetHost = feeds[feedName];
            string url = "http://" + targetHost;

            if (isFeedCall) url += "/nuget";
            url += action + context.Request.QueryString;            

            var proxyRequest = new HttpRequestMessage();

            var method = context.Request.Method;
            if (!HttpMethods.IsGet(method) &&
                !HttpMethods.IsHead(method) &&
                !HttpMethods.IsDelete(method) &&
                !HttpMethods.IsTrace(method))
            {
                var streamContent = new StreamContent(context.Request.Body);
                proxyRequest.Content = streamContent;
            }

            foreach (var header in context.Request.Headers)
            {
                if (header.Key == "Host") continue;

                if (!proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && proxyRequest.Content != null)
                {
                    proxyRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            proxyRequest.RequestUri = new Uri(url);
            proxyRequest.Method = new HttpMethod(method);

            var response = await httpClient.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            context.Response.Headers.Remove("transfer-encoding");
            await response.Content.CopyToAsync(context.Response.Body);
        }

        public async Task PushPackage(Stream fileStream, string targetFeed)
        {
            if (!feeds.ContainsKey(targetFeed))
            {
                throw new InvalidOperationException("Feed does not exist.");
            }

            string targetUri = "http://" + feeds[targetFeed] + "/api/v2/package";

            using (HttpClient httpClient = new HttpClient())
            {
                await httpClient.PutAsync(targetUri, new StreamContent(fileStream));
            }
        }
    }
}
