﻿using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Utils;
using YoutubeExplode.Utils.Extensions;

namespace YoutubeExplode;

internal class YoutubeHttpHandler : ClientWrappingHttpHandler
{
    public YoutubeHttpHandler(HttpClient http, bool disposeClient = false)
        : base(http, disposeClient)
    {
    }

    private async ValueTask<HttpResponseMessage> SendCoreAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        // Set the API key if necessary
        if (request.RequestUri is not null &&
            request.RequestUri.AbsolutePath.StartsWith("/youtubei/") &&
            !UrlEx.ContainsQueryParameter(request.RequestUri.Query, "key"))
        {
            request.RequestUri = new Uri(
                UrlEx.SetQueryParameter(
                    request.RequestUri.OriginalString,
                    "key",
                    // This key doesn't appear to change
                    "AIzaSyA8eiZmM1FaDVjRy-df2KTyQ_vz_yYM39w"
                )
            );
        }

        // Set the language if necessary
        if (request.RequestUri is not null && !UrlEx.ContainsQueryParameter(request.RequestUri.Query, "hl"))
        {
            request.RequestUri = new Uri(
                UrlEx.SetQueryParameter(
                    request.RequestUri.OriginalString,
                    "hl",
                    "en"
                )
            );
        }

        // Set the user agent if necessary
        if (!request.Headers.Contains("User-Agent"))
        {
            request.Headers.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/89.0.4389.114 Safari/537.36"
            );
        }

        // Set required cookies
        request.Headers.Add("Cookie", "CONSENT=YES+cb; YSC=DwKYllHNwuw");

        var response = await base.SendAsync(request, cancellationToken);

        // Special case check for rate limiting errors
        if ((int)response.StatusCode == 429)
        {
            throw new RequestLimitExceededException(
                "Exceeded request rate limit. " +
                "Please try again in a few hours. " +
                "Alternatively, inject an instance of HttpClient that includes cookies for a pre-authenticated user."
            );
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        for (var retriesRemaining = 5;; retriesRemaining--)
        {
            try
            {
                using var clonedRequest = request.Clone();
                var response = await SendCoreAsync(clonedRequest, cancellationToken);

                // Retry on 5XX errors
                if ((int)response.StatusCode >= 500 && retriesRemaining > 0)
                {
                    response.Dispose();
                    continue;
                }

                return response;
            }
            // Retry on connectivity issues
            catch (HttpRequestException) when (retriesRemaining > 0)
            {
            }
        }
    }
}