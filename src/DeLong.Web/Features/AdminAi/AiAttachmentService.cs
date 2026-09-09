using System.IO.Compression;
using System.Text;
using System.Xml;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.AdminAi;

public sealed class AiAttachmentService(AppDbContext db)
{
    private const long MaxFileBytes = 15 * 1024 * 1024;
    private const long MaxRequestBytes = 30 * 1024 * 1024;
    private const int MaxFiles = 10;
    private const int MaxExtractedCharacters = 100_000;

    public async Task<(IReadOnlyList<AiAttachmentDto>? Value, string? Error)> UploadAsync(
        Guid propertyId, Guid userId, Guid conversationId, IFormFileCollection files, CancellationToken ct)
    {
        var ownsConversation = await db.AiConversations.AsNoTracking()
            .AnyAsync(x => x.Id == conversationId && x.PropertyId == propertyId && x.UserId == userId, ct);
        if (!ownsConversation) return (null, "Không tìm thấy cuộc trò chuyện.");
        if (files.Count is < 1 or > MaxFiles) return (null, $"Mỗi lần được đính kèm từ 1 đến {MaxFiles} tệp.");
        if (files.Any(x => x.Length is <= 0 or > MaxFileBytes) || files.Sum(x => x.Length) > MaxRequestBytes)
            return (null, "Mỗi tệp tối đa 15 MB và tổng mỗi lần tải tối đa 30 MB.");

        var created = new List<AiAttachment>();
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file.FileName).Trim();
            if (fileName.Length is < 1 or > 240) return (null, "Tên tệp không hợp lệ.");
            await using var source = file.OpenReadStream();
            await using var buffer = new MemoryStream((int)file.Length);
            await source.CopyToAsync(buffer, ct);
            var content = buffer.ToArray();
            var contentType = DetectType(fileName, content);
            if (contentType is null) return (null, $"Tệp '{fileName}' không được hỗ trợ. Chỉ nhận DOCX, PDF, JPG, PNG và WEBP.");
            string? extractedText = null;
            if (contentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            {
                try { extractedText = ExtractDocx(content); }
                catch (Exception ex) when (ex is InvalidDataException or XmlException)
                {
                    return (null, $"Tệp Word '{fileName}' bị lỗi hoặc không đúng định dạng DOCX.");
                }
                if (string.IsNullOrWhiteSpace(extractedText)) return (null, $"Không đọc được nội dung Word của '{fileName}'.");
            }
            created.Add(new AiAttachment
            {
                ConversationId = conversationId,
                UploadedByUserId = userId,
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = content.LongLength,
                Content = content,
                ExtractedText = extractedText
            });
        }
        db.AiAttachments.AddRange(created);
        var conversation = await db.AiConversations.SingleAsync(x => x.Id == conversationId, ct);
        conversation.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (created.Select(ToDto).ToArray(), null);
    }

    public async Task<IReadOnlyList<AiAttachmentDto>> ListAsync(Guid propertyId, Guid userId, Guid conversationId, CancellationToken ct) =>
        await db.AiAttachments.AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.Conversation.PropertyId == propertyId && x.Conversation.UserId == userId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new AiAttachmentDto(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.CreatedAtUtc))
            .ToListAsync(ct);

    private static string? DetectType(string fileName, byte[] content)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".pdf" && StartsWith(content, "%PDF-"u8)) return "application/pdf";
        if (extension is ".jpg" or ".jpeg" && content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff) return "image/jpeg";
        if (extension == ".png" && StartsWith(content, new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })) return "image/png";
        if (extension == ".webp" && content.Length >= 12 && Encoding.ASCII.GetString(content, 0, 4) == "RIFF" && Encoding.ASCII.GetString(content, 8, 4) == "WEBP") return "image/webp";
        if (extension == ".docx" && content.Length >= 4 && content[0] == 0x50 && content[1] == 0x4b) return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        return null;
    }

    private static bool StartsWith(byte[] content, ReadOnlySpan<byte> prefix) => content.AsSpan().StartsWith(prefix);

    private static string ExtractDocx(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("DOCX không có document.xml.");
        if (entry.Length > 5 * 1024 * 1024) throw new InvalidDataException("Nội dung DOCX quá lớn.");
        using var xmlStream = entry.Open();
        using var reader = XmlReader.Create(xmlStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var builder = new StringBuilder();
        while (reader.Read() && builder.Length < MaxExtractedCharacters)
        {
            if (reader.NodeType == XmlNodeType.Text) builder.Append(reader.Value);
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName is "p" or "tr") builder.AppendLine();
            else if (reader.NodeType == XmlNodeType.Element && reader.IsEmptyElement && reader.LocalName is "tab" or "br") builder.Append(' ');
        }
        return builder.ToString().Trim();
    }

    private static AiAttachmentDto ToDto(AiAttachment x) => new(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.CreatedAtUtc);
}
