﻿// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.DependencyInjection.Annotation.HttpClient;
using Snap.Hutao.Model.Entity;
using Snap.Hutao.Web.Hoyolab.Annotation;
using Snap.Hutao.Web.Hoyolab.DynamicSecret;
using Snap.Hutao.Web.Response;
using System.Net.Http;
using System.Net.Http.Json;

namespace Snap.Hutao.Web.Hoyolab.Passport;

/// <summary>
/// 通行证客户端
/// </summary>
[HighQuality]
[UseDynamicSecret]
[HttpClient(HttpClientConfiguration.XRpc2)]
internal sealed class PassportClient
{
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions options;
    private readonly ILogger<PassportClient> logger;

    /// <summary>
    /// 构造一个新的通行证客户端
    /// </summary>
    /// <param name="httpClient">http客户端</param>
    /// <param name="options">json序列化选项</param>
    /// <param name="logger">日志器</param>
    public PassportClient(HttpClient httpClient, JsonSerializerOptions options, ILogger<PassportClient> logger)
    {
        this.httpClient = httpClient;
        this.options = options;
        this.logger = logger;
    }

    /// <summary>
    /// 异步验证 LToken
    /// </summary>
    /// <param name="user">用户</param>
    /// <param name="token">取消令牌</param>
    /// <returns>验证信息</returns>
    [ApiInformation(Cookie = CookieType.LToken)]
    public async Task<Response<UserInfoWrapper>> VerifyLtokenAsync(User user, CancellationToken token)
    {
        Response<UserInfoWrapper>? response = await httpClient
            .SetUser(user, CookieType.LToken)
            .TryCatchPostAsJsonAsync<Timestamp, Response<UserInfoWrapper>>(ApiEndpoints.AccountVerifyLtoken, new(), options, logger, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(response);
    }

    /// <summary>
    /// V1 SToken 登录
    /// </summary>
    /// <param name="stokenV1">v1 SToken</param>
    /// <param name="token">取消令牌</param>
    /// <returns>登录数据</returns>
    [ApiInformation(Salt = SaltType.PROD)]
    public async Task<Response<LoginResult>> LoginBySTokenAsync(Cookie stokenV1, CancellationToken token)
    {
        HttpResponseMessage message = await httpClient
            .SetHeader("Cookie", stokenV1.ToString())
            .UseDynamicSecret(DynamicSecretVersion.Gen2, SaltType.PROD, true)
            .PostAsync(ApiEndpoints.AccountGetSTokenByOldToken, null, token)
            .ConfigureAwait(false);

        Response<LoginResult>? resp = await message.Content
            .ReadFromJsonAsync<Response<LoginResult>>(options, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    private class Timestamp
    {
        [JsonPropertyName("t")]
        public long Time { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}