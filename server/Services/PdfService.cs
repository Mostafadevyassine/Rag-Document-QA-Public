using System.Text;
using UglyToad.PdfPig;

// Extracts text from a PDF (PdfPig) and splits it into overlapping chunks.
public class PdfService
{
    public string ExtractText(Stream stream)
    {
        using var doc = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        // Reconstruct text from words joined by spaces. page.Text uses raw
        // internal content order, which mashes words/columns together (the
        // PdfPig docs explicitly warn against it) and wrecks the embeddings —
        // so retrieval matches poorly on every question. GetWords() restores
        // proper word boundaries.
        foreach (var page in doc.GetPages())
        {
            var line = string.Join(" ", page.GetWords().Select(w => w.Text));
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    public static List<string> Chunk(string text, int size = 800, int overlap = 100)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        for (int i = 0; i < text.Length; i += size - overlap)
        {
            var piece = text.Substring(i, Math.Min(size, text.Length - i)).Trim();
            if (piece.Length > 0) chunks.Add(piece);   // skip blank/whitespace-only chunks
        }
        return chunks;
    }
}
