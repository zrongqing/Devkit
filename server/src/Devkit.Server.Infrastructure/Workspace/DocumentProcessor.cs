using System.IO.Compression;
using System.Text.RegularExpressions;
using Devkit.Server.Application.Workspace;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Devkit.Server.Infrastructure.Workspace;

public sealed class DocumentProcessor(IFileService files) : IDocumentProcessor
{
    public async Task<ExtractedDocument> ExtractAsync(Guid fileId,CancellationToken ct)
    {
        var file=await files.OpenInternalAsync(fileId,ct);await using var stream=file.Stream;
        var blocks=new List<TextBlock>();var warning="";
        if(Path.GetExtension(file.Name).Equals(".pdf",StringComparison.OrdinalIgnoreCase))
        {
            using var pdf=PdfDocument.Open(stream);
            if(pdf.NumberOfPages>500) throw new BusinessException(400,"too_many_pages","首版单文件最多 500 页，请拆分上传。");
            var empty=new List<int>();var heading="";
            foreach(var page in pdf.GetPages())
            {
                ct.ThrowIfCancellationRequested();var text=ContentOrderTextExtractor.GetText(page);
                if(string.IsNullOrWhiteSpace(text)){empty.Add(page.Number);continue;}
                var lines=text.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                foreach(var line in lines)
                {
                    if(IsHeading(line))heading=line;
                    blocks.Add(new TextBlock(line,heading,$"第 {page.Number} 页",page.Number));
                }
            }
            if(empty.Count>0)warning=$"第 {string.Join("、",empty)} 页未提取到文字，可能需要 OCR；这些页面未建立索引。";
        }
        else
        {
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,true))
            {
                if(zip.Entries.Count>5000 || zip.Entries.Sum(x=>x.Length)>100L*1024*1024) throw new BusinessException(400,"expanded_document_too_large","DOCX 解压内容超出限制。");
                if(zip.GetEntry("word/document.xml") is null)throw new BusinessException(415,"invalid_docx","不是有效的 Word 文档。");
            }
            stream.Position=0;using var document=WordprocessingDocument.Open(stream,false);
            var body=document.MainDocumentPart?.Document?.Body ?? throw new BusinessException(400,"empty_document","文档没有正文。");
            var heading="";var paragraph=0;var table=0;
            foreach(var element in body.ChildElements)
            {
                ct.ThrowIfCancellationRequested();
                if(element is W.Paragraph p)
                {
                    paragraph++;var text=ParagraphText(p);if(string.IsNullOrWhiteSpace(text))continue;
                    var style=p.ParagraphProperties?.ParagraphStyleId?.Val?.Value??"";
                    if(style.StartsWith("Heading",StringComparison.OrdinalIgnoreCase) || style.Contains("标题") || IsHeading(text)) heading=text;
                    blocks.Add(new TextBlock(text,heading,$"{heading} / 段落 {paragraph}".Trim(' ','/'),null));
                }
                else if(element is W.Table t)
                {
                    table++;var row=0;string header="";
                    foreach(var tr in t.Elements<W.TableRow>())
                    {
                        row++;var text=string.Join(" | ",tr.Elements<W.TableCell>().Select(c=>string.Join(" ",c.Elements<W.Paragraph>().Select(ParagraphText))));
                        if(row==1)header=text;
                        blocks.Add(new TextBlock(row>1?$"表头：{header}\n{text}":text,heading,$"表格 {table} / 行 {row}",null));
                    }
                }
            }
        }
        if(blocks.Count==0)throw new BusinessException(400,"no_text","未提取到文字；扫描文件需要先进行 OCR。");
        if(blocks.Sum(x=>x.Text.Length)>2_000_000)throw new BusinessException(400,"text_too_large","文档文字过多，请拆分上传。");
        return new ExtractedDocument(blocks,warning);
    }
    private static string ParagraphText(W.Paragraph p)=>string.Concat(p.Descendants().Select(x=>x is W.Text t?t.Text:x is W.TabChar?"\t":x is W.Break?"\n":"")).Trim();
    private static bool IsHeading(string text)=>text.Length<150 && Regex.IsMatch(text,@"^(第[零〇一二三四五六七八九十百千\d]+[章节条款]|[一二三四五六七八九十]+、|\d+(\.\d+)*[、.\s])");
    public IReadOnlyList<TextBlock> Split(IReadOnlyList<TextBlock> blocks)
    {
        var output=new List<TextBlock>();TextBlock? current=null;
        foreach(var b in blocks)
        {
            foreach(var piece in Pieces(b.Text,1000))
            {
                var next=b with{Text=piece};
                if(current is not null && current.Heading==next.Heading && current.Page==next.Page && current.Text.Length+piece.Length<600 && !IsHeading(piece)) current=current with{Text=current.Text+"\n"+piece};
                else{if(current is not null)output.Add(current);current=next;}
            }
        }
        if(current is not null)output.Add(current);return output;
    }
    private static IEnumerable<string> Pieces(string text,int max)
    {
        var start=0;
        while(start<text.Length)
        {
            var length=Math.Min(max,text.Length-start);
            if(start+length<text.Length)
            {
                var boundary=text.LastIndexOfAny(['。','；','\n','！','？'],start+length-1,length/2);
                if(boundary>start)length=boundary-start+1;
            }
            yield return text.Substring(start,length);
            start+=length;if(start<text.Length && length>200)start-=100;
        }
    }
}
