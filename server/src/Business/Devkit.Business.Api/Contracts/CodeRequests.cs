using System.Text.Json.Serialization;
using Devkit.Business.Application.CodeSystem;

namespace Devkit.Business.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCodeRule(string RuleKey, string Name, CodeDefinition Definition);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveCodeDraft(long ExpectedRevision, CodeDefinition Definition);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CodeTransition(long ExpectedRevision, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewCode(string RuleKey, CodeDefinition Definition, string SampleAt, long SampleValue);
public sealed record CodeChangeResult(Guid Id, long Revision, string Status, int? ActiveVersionNo);
