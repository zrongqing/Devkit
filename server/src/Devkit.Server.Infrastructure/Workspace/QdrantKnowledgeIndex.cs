using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.Workspace;
using Microsoft.Extensions.Options;

namespace Devkit.Server.Infrastructure.Workspace;

public sealed class QdrantKnowledgeIndex(IHttpClientFactory factory,IOptions<WorkspaceOptions> options,IModelGateway model) : IKnowledgeIndex
{
    private const string Lexical="devkit_study_lexical_v1";
    private string Dense=>"devkit_study_dense_"+model.EmbeddingProfile;
    private async Task<JsonDocument> SendAsync(HttpMethod method,string path,object? body,CancellationToken ct,bool allowExists=false)
    {
        using var client=factory.CreateClient();client.Timeout=TimeSpan.FromSeconds(20);
        using var request=new HttpRequestMessage(method,options.Value.QdrantUrl.TrimEnd('/')+path);
        if(body is not null)request.Content=JsonContent.Create(body);
        if(!string.IsNullOrWhiteSpace(options.Value.QdrantApiKey))request.Headers.Add("api-key",options.Value.QdrantApiKey);
        using var response=await client.SendAsync(request,ct);
        if(!response.IsSuccessStatusCode && !(allowExists && response.StatusCode==HttpStatusCode.Conflict))throw new BusinessException(503,"index_unavailable",$"检索服务返回 {(int)response.StatusCode}，请检查 Qdrant。");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    public async Task<bool> AvailableAsync(CancellationToken ct)
    {
        try{using var json=await SendAsync(HttpMethod.Get,"/collections",null,ct);return true;}catch(Exception e)when(e is HttpRequestException or BusinessException or TaskCanceledException){return false;}
    }
    private async Task EnsureAsync(string collection,int? dimensions,CancellationToken ct)
    {
        using var all=await SendAsync(HttpMethod.Get,"/collections",null,ct);
        if(!all.RootElement.GetProperty("result").GetProperty("collections").EnumerateArray().Any(x=>x.GetProperty("name").GetString()==collection))
        {
            object body=dimensions.HasValue?new{vectors=new{size=dimensions.Value,distance="Cosine"}}:new{ sparse_vectors=new Dictionary<string,object>{{"text",new{modifier="idf"}}}};
            using var created=await SendAsync(HttpMethod.Put,$"/collections/{collection}",body,ct,true);
            foreach(var field in new[]{"ownerId","knowledgeBaseId","sourceId","versionId"})
            {using var indexed=await SendAsync(HttpMethod.Put,$"/collections/{collection}/index?wait=true",new{field_name=field,field_schema="keyword"},ct);}
        }
        if(dimensions.HasValue)
        {
            using var info=await SendAsync(HttpMethod.Get,$"/collections/{collection}",null,ct);
            if(info.RootElement.GetProperty("result").GetProperty("config").GetProperty("params").GetProperty("vectors").GetProperty("size").GetInt32()!=dimensions.Value)throw new BusinessException(409,"embedding_dimension_changed","模型向量维度已变化，请更新模型配置名称并重新建立索引。");
        }
    }
    private static object Bm25(string text)=>new{text,model="qdrant/bm25",options=new{tokenizer="multilingual",stemmer=new{type="none"},stopwords=new Dictionary<string,object>()}};
    public async Task IndexAsync(IReadOnlyList<KnowledgeChunk> chunks,CancellationToken ct)
    {
        await EnsureAsync(Lexical,null,ct);
        foreach(var batch in chunks.Chunk(32))
        {
            using var lexical=await SendAsync(HttpMethod.Put,$"/collections/{Lexical}/points?wait=true",new{points=batch.Select(c=>new{id=c.Id,vector=new Dictionary<string,object>{{"text",Bm25(c.Heading+"\n"+c.Text)}},payload=new{ownerId=c.OwnerId.ToString(),knowledgeBaseId=c.KnowledgeBaseId.ToString(),sourceId=c.SourceId.ToString(),versionId=c.VersionId.ToString()}})},ct);
        }
    }
    public async Task IndexDenseAsync(IReadOnlyList<KnowledgeChunk> chunks,CancellationToken ct)
    {
        if(!model.EmbeddingConfigured)throw new BusinessException(503,"model_not_configured","向量模型尚未配置，关键词查询已可用。");
        foreach(var batch in chunks.Chunk(32))
        {
            var vectors=await model.EmbedAsync(batch.Select(c=>c.Heading+"\n"+c.Text).ToArray(),ct);await EnsureAsync(Dense,vectors[0].Length,ct);
            using var response=await SendAsync(HttpMethod.Put,$"/collections/{Dense}/points?wait=true",new{points=batch.Select((c,i)=>new{id=c.Id,vector=vectors[i],payload=new{ownerId=c.OwnerId.ToString(),knowledgeBaseId=c.KnowledgeBaseId.ToString(),sourceId=c.SourceId.ToString(),versionId=c.VersionId.ToString()}})},ct);
            foreach(var chunk in batch)chunk.EmbeddingProfile=model.EmbeddingProfile;
        }
    }
    public async Task<IReadOnlyList<(Guid Id,double Score)>> SearchAsync(string query,string mode,Guid ownerId,IReadOnlyList<Guid> kbIds,IReadOnlyList<Guid> versionIds,Guid? sourceId,int limit,CancellationToken ct)
    {
        if(kbIds.Count==0||versionIds.Count==0)return [];
        var must=new List<object>{new{key="ownerId",match=new{value=ownerId.ToString()}},new{key="knowledgeBaseId",match=new{any=kbIds.Select(x=>x.ToString()).ToArray()}}};
        if(sourceId.HasValue)must.Add(new{key="sourceId",match=new{value=sourceId.Value.ToString()}});
        must.Add(new{key="versionId",match=new{any=versionIds.Select(x=>x.ToString()).ToArray()}});
        var filter=new{must};
        using var lex=await SendAsync(HttpMethod.Post,$"/collections/{Lexical}/points/query",new{query=Bm25(query),@using="text",filter,limit,with_payload=false},ct);
        var first=Parse(lex);if(mode=="keyword")return first;
        var vector=(await model.EmbedAsync([query],ct))[0];await EnsureAsync(Dense,vector.Length,ct);
        using var dense=await SendAsync(HttpMethod.Post,$"/collections/{Dense}/points/query",new{query=vector,filter,limit,with_payload=false},ct);
        var scores=new Dictionary<Guid,double>();
        foreach(var list in new[]{first,Parse(dense)})for(var i=0;i<list.Count;i++)scores[list[i].Id]=scores.GetValueOrDefault(list[i].Id)+1d/(60+i+1);
        return scores.OrderByDescending(x=>x.Value).Select(x=>(x.Key,x.Value)).ToArray();
    }
    private static List<(Guid Id,double Score)> Parse(JsonDocument json)=>json.RootElement.GetProperty("result").GetProperty("points").EnumerateArray().Select(x=>(Guid.Parse(x.GetProperty("id").GetString()!),x.GetProperty("score").GetDouble())).ToList();
    public async Task DeleteAsync(Guid sourceId,CancellationToken ct)
    {
        using var all=await SendAsync(HttpMethod.Get,"/collections",null,ct);
        foreach(var name in all.RootElement.GetProperty("result").GetProperty("collections").EnumerateArray().Select(x=>x.GetProperty("name").GetString()!).Where(x=>x.StartsWith("devkit_study_",StringComparison.Ordinal)))
        {using var deleted=await SendAsync(HttpMethod.Post,$"/collections/{name}/points/delete?wait=true",new{filter=new{must=new[]{new{key="sourceId",match=new{value=sourceId.ToString()}}}}},ct);}
    }
}
