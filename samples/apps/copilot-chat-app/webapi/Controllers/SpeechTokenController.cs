﻿// Copyright (c) Microsoft. All rights reserved.

using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SemanticKernel.Service.Config;
using SemanticKernel.Service.Model;

namespace SemanticKernel.Service.Controllers;

[Authorize]
[ApiController]
public class SpeechTokenController : ControllerBase
{
    private sealed class TokenResult
    {
        public string? Token { get; set; }
        public HttpStatusCode? ResponseCode { get; set; }
    }

    private readonly ILogger<SpeechTokenController> _logger;
    private readonly AzureSpeechOptions _options;

    public SpeechTokenController(IOptions<AzureSpeechOptions> options, ILogger<SpeechTokenController> logger)
    {
        this._logger = logger;
        this._options = options.Value;
    }

    /// <summary>
    /// Get an authorization token and region
    /// </summary>
    [Route("speechToken")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeechTokenResponse>> GetAsync()
    {
        if (string.IsNullOrWhiteSpace(this._options.Region))
        {
            throw new InvalidOperationException($"Missing value for {AzureSpeechOptions.PropertyName}:{nameof(this._options.Region)}");
        }

        string fetchTokenUri = "https://" + this._options.Region + ".api.cognitive.microsoft.com/sts/v1.0/issueToken";

        TokenResult tokenResult = await this.FetchTokenAsync(fetchTokenUri, this._options.Key);
        var isSuccess = tokenResult.ResponseCode != HttpStatusCode.NotFound;
        return new SpeechTokenResponse { Token = tokenResult.Token, Region = this._options.Region, IsSuccess = isSuccess };
    }

    private async Task<TokenResult> FetchTokenAsync(string fetchUri, string subscriptionKey)
    {
        // TODO: get the HttpClient from the DI container
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
        UriBuilder uriBuilder = new(fetchUri);

        var result = await client.PostAsync(uriBuilder.Uri, null);
        if (result.IsSuccessStatusCode)
        {
            var response = result.EnsureSuccessStatusCode();
            this._logger.LogDebug("Token Uri: {0}", uriBuilder.Uri.AbsoluteUri);
            string token = await result.Content.ReadAsStringAsync();
            return new TokenResult { Token = token, ResponseCode = response.StatusCode };
        }
        else
        {
            return new TokenResult { Token = "", ResponseCode = HttpStatusCode.NotFound };
        }
    }
}
