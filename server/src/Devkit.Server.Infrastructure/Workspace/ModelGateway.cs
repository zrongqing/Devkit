using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devkit.Server.Application.Workspace;
using Microsoft.Extensions.Options;

namespace Devkit.Server.Infrastructure.Workspace;

public sealed class ModelGateway(IHttpClientFactory factory,IOptions<WorkspaceOptions> options) : IModelGateway
{
    public bool ChatConfigured=>options.Value.Chat.Configured;
    public bool EmbeddingConfigured=>options.Value.Embedding.Configured;
    public string EmbeddingProfile=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.Embedding.BaseUrl+"|"+options.Value.Embedding.Model))).ToLowerInvariant()[..16];
    private async Task<JsonDocument> SendAsync(ModelOptions model,string path,object body,CancellationToken ct)
    {
        if(!model.Configured)throw new BusinessException(503,"model_not_configured","请配置对应模型的服务地址和模型名。");
        using var client=factory.CreateClient();client.Timeout=TimeSpan.FromSeconds(Math.Clamp(model.TimeoutSeconds,5,300));
        using var request=new HttpRequestMessage(HttpMethod.Post,model.BaseUrl.TrimEnd('/')+"/"+path){Content=JsonContent.Create(body)};
        if(!string.IsNullOrWhiteSpace(model.ApiKey))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",model.ApiKey);
        using var response=await client.SendAsync(request,ct);
        if(!response.IsSuccessStatusCode)throw new BusinessException(503,"model_unavailable",$"模型服务返回 {(int)response.StatusCode}，请检查配置或稍后重试。");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    public async Task<float[][]> EmbedAsync(IReadOnlyList<string> input,CancellationToken ct)
    {
        var result=new List<float[]>();
        foreach(var batch in input.Chunk(32))
        {
            using var json=await SendAsync(options.Value.Embedding,"embeddings",new{model=options.Value.Embedding.Model,input=batch},ct);
            var vectors=json.RootElement.GetProperty("data").EnumerateArray().OrderBy(x=>x.GetProperty("index").GetInt32()).Select(x=>x.GetProperty("embedding").EnumerateArray().Select(v=>v.GetSingle()).ToArray()).ToArray();
            if(vectors.Length!=batch.Length || vectors.Any(v=>v.Length==0 || v.Any(x=>!float.IsFinite(x))) || vectors.Select(v=>v.Length).Distinct().Count()!=1)throw new BusinessException(503,"invalid_embeddings","向量模型返回的数据格式不正确。");
            result.AddRange(vectors);
        }
        return result.ToArray();
    }
    public async Task<string> ChatAsync(string instruction,string content,CancellationToken ct)
    {
        using var json=await SendAsync(options.Value.Chat,"chat/completions",new{model=options.Value.Chat.Model,temperature=0.1,messages=new[]{new{role="system",content=instruction},new{role="user",content}}},ct);
        return json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??"";
    }
}
