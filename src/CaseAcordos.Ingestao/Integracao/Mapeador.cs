using CaseAcordos.Contratos.Mensagens;
using CaseAcordos.Ingestao.DTO;

namespace CaseAcordos.Ingestao.Integracao;

/// <summary>
/// Única fronteira de tradução: jsonplaceholder (users/albums/photos) -> domínio (autor/acordo/documento).
/// De-para conforme a IMG_5822:
/// users.id/name/email/phone -> autor.id/nome/email/telefone;
/// users.address (street, suite, city, zipcode) -> endereço; users.company.name -> empresa.nome;
/// users.company.bs -> empresa.segmento; albums.id/title -> acordo.id/titulo;
/// photos.id/title/url/thumbnailUrl -> documento.id/titulo/url/thumbnailUrl.
/// </summary>
public static class Mapeador
{
    public static AutorMensagem ParaAutor(UsuarioExternoDTO usuario) => new(
        Id: usuario.Id,
        Nome: usuario.Name ?? string.Empty,
        Email: usuario.Email ?? string.Empty,
        Telefone: usuario.Phone,
        EnderecoRua: usuario.Address?.Street,
        EnderecoComplemento: usuario.Address?.Suite,
        EnderecoCidade: usuario.Address?.City,
        EnderecoCep: usuario.Address?.Zipcode,
        EmpresaNome: usuario.Company?.Name,
        EmpresaSegmento: usuario.Company?.Bs);

    public static AcordoMensagem ParaAcordo(AcordoExternoDTO album) => new(album.Id, album.Title ?? string.Empty);

    public static DocumentoMensagem ParaDocumento(DocumentoExternoDTO foto) => new(
        foto.Id,
        foto.Title ?? string.Empty,
        foto.Url ?? string.Empty,
        foto.ThumbnailUrl ?? string.Empty);

    public static MensagemAcordo ParaMensagem(
        string correlationId,
        UsuarioExternoDTO usuario,
        AcordoExternoDTO album,
        IEnumerable<DocumentoExternoDTO> fotos) => new(
            correlationId,
            ParaAutor(usuario),
            ParaAcordo(album),
            fotos.Select(ParaDocumento).ToList());
}
