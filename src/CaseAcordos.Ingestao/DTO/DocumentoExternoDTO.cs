namespace CaseAcordos.Ingestao.DTO;

public sealed record DocumentoExternoDTO(int AlbumId, int Id, string? Title, string? Url, string? ThumbnailUrl);