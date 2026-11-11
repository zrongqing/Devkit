using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Devkit.Business.Application.Workspace;

namespace Devkit.Business.Application.CodeSystem;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CodeSegment(string Type, string? Text = null, int? Width = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CodeDefinition(int SchemaVersion, string UtcOffset, string Reset, long Start, long Step, CodeSegment[] Segments)
{
    public void Validate(CodeDefinition? active = null)
    {
        if (SchemaVersion != 1 || UtcOffset != "+08:00") throw Invalid("schemaVersion / utcOffset", "仅支持版本 1 和 +08:00。");
        if (Reset is not ("none" or "year" or "month" or "day")) throw Invalid("reset", "重置周期无效。");
        if (Segments is not { Length: >= 1 and <= 16 }) throw Invalid("segments", "需要 1 至 16 个编码段。");
        var seen = new HashSet<string>();
        var length = 0;
        for (var i = 0; i < Segments.Length; i++)
        {
            var segment = Segments[i];
            var field = $"segments[{i}]";
            if (segment == null) throw Invalid(field, "编码段不能为空。");
            if (segment.Type != "literal" && !seen.Add(segment.Type)) throw Invalid(field, "该段类型重复。");
            length += segment switch
            {
                { Type: "literal", Width: null, Text: not null } when Regex.IsMatch(segment.Text, @"\A[A-Z0-9_./-]{1,32}\z") => segment.Text.Length,
                { Type: "sequence", Text: null, Width: >= 1 and <= 12 } => segment.Width.Value,
                { Type: "year", Text: null, Width: null } => 4,
                { Type: "month" or "day", Text: null, Width: null } => 2,
                _ => throw Invalid(field, "段类型、文本或位宽无效。")
            };
        }
        if (length > 64 || !seen.Contains("sequence")) throw Invalid("segments", "必须含一段流水，编码总长最多 64。");
        var required = Reset switch { "year" => new[] { "year" }, "month" => ["year", "month"], "day" => ["year", "month", "day"], _ => [] };
        if (required.Any(x => !seen.Contains(x))) throw Invalid("reset", "日期段未覆盖重置周期。");
        if (Start < 1 || Step < 1 || Start > MaxValue || Step > MaxValue) throw Invalid("start / step", "起值和步长必须为位宽范围内的正整数。");
        if (active != null && (Start != active.Start || Step != active.Step || Reset != active.Reset || Width < active.Width))
            throw Invalid("definition", "发布后须保留计数策略，位宽只能扩大。");
    }

    [JsonIgnore] public int Width => Segments.Single(x => x.Type == "sequence").Width!.Value;
    [JsonIgnore] public long MaxValue => (long)Math.Pow(10, Width) - 1;

    public CodePreview Render(DateTimeOffset instant, long value)
    {
        if (value < 1 || value > MaxValue) throw Invalid("sampleValue", "序号超出位宽范围。");
        if (instant.UtcDateTime > DateTime.MaxValue.AddHours(-8))
            throw Invalid("sampleAt", "转换后的业务年份超出范围。");
        var date = instant.ToOffset(TimeSpan.FromHours(8));
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        foreach (var segment in Segments)
            text.Append(segment.Type switch
            {
                "literal" => segment.Text,
                "year" => date.ToString("yyyy", culture),
                "month" => date.ToString("MM", culture),
                "day" => date.ToString("dd", culture),
                "sequence" => value.ToString($"D{Width}", culture),
                _ => throw Invalid("segments", "段类型无效。")
            });
        var period = Reset switch { "year" => "yyyy", "month" => "yyyyMM", "day" => "yyyyMMdd", _ => null };
        return new(text.ToString(), period == null ? "ALL" : date.ToString(period, culture));
    }

    public static BusinessException Invalid(string field, string message) => new(400, "invalid_rule", $"{field}：{message}") { Field = field };
}

public sealed record CodePreview(string Code, string PeriodKey);
