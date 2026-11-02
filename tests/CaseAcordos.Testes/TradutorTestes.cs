using CaseAcordos.Contratos.Mensagens;
using CaseAcordos.Ingestao.DTO;
using CaseAcordos.Ingestao.Integracao;

namespace CaseAcordos.Testes;

public sealed class TradutorTestes
{
    [Fact]
    public void Traduz_autor_com_endereco_e_empresa_conforme_de_para()
    {
        var usuario = new UsuarioExternoDTO(
            5, "Chelsey Dietrich", "Lucio_Hettinger@annie.ca", "(254)954-1289",
            new EnderecoExternoDTO("Skiles Walks", "Suite 351", "Roscoeview", "33263"),
            new EmpresaExternaDTO("Keebler LLC", "revolutionize end-to-end systems"));

        var autor = Mapeador.ParaAutor(usuario);

        Assert.Equal(5, autor.Id);
        Assert.Equal("Chelsey Dietrich", autor.Nome);
        Assert.Equal("Lucio_Hettinger@annie.ca", autor.Email);
        Assert.Equal("(254)954-1289", autor.Telefone);
        Assert.Equal("Skiles Walks", autor.EnderecoRua);
        Assert.Equal("Suite 351", autor.EnderecoComplemento);
        Assert.Equal("Roscoeview", autor.EnderecoCidade);
        Assert.Equal("33263", autor.EnderecoCep);
        Assert.Equal("Keebler LLC", autor.EmpresaNome);
        Assert.Equal("revolutionize end-to-end systems", autor.EmpresaSegmento); // company.bs
    }

    [Fact]
    public void Endereco_e_empresa_ausentes_geram_campos_nulos()
    {
        var autor = Mapeador.ParaAutor(new UsuarioExternoDTO(1, "Ana", "ana@x.com", null, null, null));

        Assert.Null(autor.EnderecoRua);
        Assert.Null(autor.EnderecoCep);
        Assert.Null(autor.EmpresaNome);
        Assert.Null(autor.EmpresaSegmento);
    }

    [Fact]
    public void Traduz_album_para_acordo_e_foto_para_documento()
    {
        var acordo = Mapeador.ParaAcordo(new AcordoExternoDTO(5, 41, "ea voluptates maiores"));
        var documento = Mapeador.ParaDocumento(new DocumentoExternoDTO(41, 2001, "titulo foto", "https://u/1", "https://t/1"));

        Assert.Equal(41, acordo.Id);
        Assert.Equal("ea voluptates maiores", acordo.Titulo);
        Assert.Equal(2001, documento.Id);
        Assert.Equal("titulo foto", documento.Titulo);
        Assert.Equal("https://u/1", documento.Url);
        Assert.Equal("https://t/1", documento.ThumbnailUrl);
    }

    [Fact]
    public void Mensagem_leva_correlationId_autor_acordo_e_documentos()
    {
        var mensagem = Mapeador.ParaMensagem(
            "corr-abc",
            Dados.Usuario(5),
            Dados.Acordo(5, 41),
            new[] { Dados.Documento(41, 1), Dados.Documento(41, 2) });

        Assert.Equal("corr-abc", mensagem.CorrelationId);
        Assert.Equal(5, mensagem.Autor.Id);
        Assert.Equal(41, mensagem.Acordo.Id);
        Assert.Equal(new[] { 1, 2 }, mensagem.Documentos.Select(d => d.Id).ToArray());
    }

    [Fact]
    public void Mensagem_serializa_em_camelCase_conforme_contrato()
    {
        var json = SerializadorMensagens.Serializar(Dados.Mensagem());

        Assert.Contains("\"correlationId\"", json);
        Assert.Contains("\"thumbnailUrl\"", json);
        Assert.Contains("\"empresaSegmento\"", json);

        var volta = SerializadorMensagens.Desserializar<MensagemAcordo>(json);
        Assert.NotNull(volta);
        Assert.Equal(10, volta!.Acordo.Id);
    }
}