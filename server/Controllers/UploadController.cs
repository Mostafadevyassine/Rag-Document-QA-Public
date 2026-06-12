using Microsoft.AspNetCore.Mvc;

// POST /upload — receives a PDF, indexes it, returns a chunk count.
// Thin controller: validate input, call services in order, return result.
[ApiController]
[Route("upload")]
public class UploadController : ControllerBase
{
    private readonly PdfService _pdf;
    private readonly IEmbeddingService _embed;
    private readonly IVectorStore _store;

    public UploadController(PdfService pdf, IEmbeddingService embed, IVectorStore store)
    {
        _pdf = pdf;
        _embed = embed;
        _store = store;
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest("No file.");
        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("PDF only.");

        _store.Clear();   // weekend version: one document at a time
        using var stream = file.OpenReadStream();
        var text   = _pdf.ExtractText(stream);
        var pieces = PdfService.Chunk(text);
        var vecs   = await _embed.EmbedBatch(pieces);
        _store.Add(pieces.Zip(vecs, (t, v) => new Chunk(t, v, 0)));

        return Ok(new { chunks = pieces.Count });
    }
}
